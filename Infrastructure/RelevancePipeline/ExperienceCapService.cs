using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using Application.Common.Scoring;

namespace Infrastructure.RelevancePipeline;


public sealed class ExperienceCapService : IExperienceCapService
{


    public RoleWeightedYears? ComputeRoleWeightedYears(string cvText)
    {
        if (string.IsNullOrWhiteSpace(cvText) || !cvText.TrimStart().StartsWith("{"))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(cvText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("experience", out var experienceEl))
                return null;

            var buckets = new Dictionary<RoleCategory, double>();
            var engBuckets = new Dictionary<Application.Common.Scoring.RoleBucketId, double>();

            foreach (var entry in experienceEl.EnumerateArray())
            {
                var title = entry.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? ""
                    : "";

                var type = entry.TryGetProperty("type", out var tp) && tp.ValueKind == JsonValueKind.String
                    ? tp.GetString() ?? ""
                    : "";

                if (type.Equals("COURSE", StringComparison.OrdinalIgnoreCase))
                    continue;

                var durationMonths = 0.0;
                if (entry.TryGetProperty("duration_months", out var dm) && dm.ValueKind == JsonValueKind.Number)
                    durationMonths = dm.GetDouble();

                double multiplier = type.ToUpperInvariant() switch
                {
                    "PRODUCTION"  => 1.0,
                    "FREELANCE"   => 0.7,
                    "INTERNSHIP"  => 0.5,
                    "PET_PROJECT" => 0.2,
                    _             => 1.0
                };


                var category = ClassifyRole(title);
                if (category != RoleCategory.Other)
                    buckets[category] = buckets.GetValueOrDefault(category) + durationMonths * multiplier;


                var engSubtype = ClassifyEngineeringSubtype(title);
                if (engSubtype is { } st)
                    engBuckets[st] = engBuckets.GetValueOrDefault(st) + durationMonths * multiplier;
            }


            var allBuckets = new Dictionary<Application.Common.Scoring.RoleBucketId, double>
            {
                [Application.Common.Scoring.RoleBucketId.PmPo]            = Math.Round(buckets.GetValueOrDefault(RoleCategory.PmPo)            / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.Pmm]             = Math.Round(buckets.GetValueOrDefault(RoleCategory.Pmm)             / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.BusinessAnalyst] = Math.Round(buckets.GetValueOrDefault(RoleCategory.BusinessAnalyst) / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.ProjectManager]  = Math.Round(buckets.GetValueOrDefault(RoleCategory.ProjectManager)  / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.Developer]       = Math.Round(buckets.GetValueOrDefault(RoleCategory.Developer)       / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.DataAnalyst]     = Math.Round(buckets.GetValueOrDefault(RoleCategory.DataAnalyst)     / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.Designer]        = Math.Round(buckets.GetValueOrDefault(RoleCategory.Designer)        / 12.0, 1),
                [Application.Common.Scoring.RoleBucketId.Marketing]       = Math.Round(buckets.GetValueOrDefault(RoleCategory.Marketing)       / 12.0, 1),
            };

            foreach (var (subtype, months) in engBuckets)
                allBuckets[subtype] = Math.Round(months / 12.0, 1);

            return new RoleWeightedYears(allBuckets);
        }
        catch
        {
            return null;
        }
    }


    private static Application.Common.Scoring.RoleBucketId? ClassifyEngineeringSubtype(string title)
    {
        var t = title.ToLowerInvariant();


        if (t.Contains("ml engineer") || t.Contains("machine learning engineer") ||
            t.Contains("ai engineer")) return Application.Common.Scoring.RoleBucketId.MlEngineer;

        if (t.Contains("data engineer") || t.Contains("data platform engineer"))
            return Application.Common.Scoring.RoleBucketId.DataEngineer;

        if (t.Contains("devops")    || t.Contains("sre") ||
            t.Contains("site reliability") || t.Contains("platform engineer"))
            return Application.Common.Scoring.RoleBucketId.DevOps;

        if (t.Contains("qa engineer") || t.Contains("qa automation") ||
            t.Contains("sdet")        || t.Contains("test automation") ||
            t.Contains("test engineer")) return Application.Common.Scoring.RoleBucketId.Qa;

        if (t.Contains("ios developer")     || t.Contains("android developer") ||
            t.Contains("mobile developer")  || t.Contains("react native") ||
            t.Contains("flutter developer") || t.Contains("ios engineer") ||
            t.Contains("android engineer")) return Application.Common.Scoring.RoleBucketId.Mobile;

        if (t.Contains("embedded engineer") || t.Contains("firmware engineer") ||
            t.Contains("embedded developer")) return Application.Common.Scoring.RoleBucketId.Embedded;

        if (t.Contains("security engineer") || t.Contains("appsec engineer") ||
            t.Contains("application security")) return Application.Common.Scoring.RoleBucketId.SecurityEng;

        if (t.Contains("fullstack")  || t.Contains("full-stack") ||
            t.Contains("full stack")) return Application.Common.Scoring.RoleBucketId.Fullstack;

        if (t.Contains("frontend")        || t.Contains("front-end") ||
            t.Contains("front end")       || t.Contains("react developer") ||
            t.Contains("angular developer") || t.Contains("vue developer") ||
            t.Contains("ui developer")) return Application.Common.Scoring.RoleBucketId.Frontend;

        if (t.Contains("backend")        || t.Contains("back-end") ||
            t.Contains("back end")       || t.Contains("server-side") ||
            t.Contains(".net developer") || t.Contains("java developer") ||
            t.Contains("python developer") || t.Contains("go developer") ||
            t.Contains("ruby developer")) return Application.Common.Scoring.RoleBucketId.Backend;


        return null;
    }


    public (float Score, string Reason)? TryApplyCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        RoleWeightedYears roleYears,
        bool careerSwitcher = false,
        int technicalSkillsCount = 0)
    {
        var requiredRole = ClassifyRole(jobTitle);
        if (requiredRole == RoleCategory.Other)
            return null;

        var requiredYears = ExtractMinRequiredYears(jobDescription);
        if (requiredYears <= 0)
            return null;

        var candidateYears = GetEffectiveCandidateYears(roleYears, requiredRole, careerSwitcher, technicalSkillsCount);

        int? maxScore     = null;
        string? forcedVerdict = null;

        if (requiredYears >= 5 && candidateYears <= 2)
        {
            maxScore      = 22;
            forcedVerdict = "weak_fit";
        }
        else if (requiredYears >= 3 && candidateYears <= 1)
        {
            maxScore      = 32;
            forcedVerdict = "weak_fit";
        }
        else if (requiredYears >= 2 && candidateYears == 0)
        {
            maxScore      = 30;
            forcedVerdict = "weak_fit";
        }
        else if (requiredYears >= 2 && candidateYears < 1)
        {
            maxScore = 52;
        }
        else if (requiredYears == 1 && candidateYears == 0)
        {
            maxScore = 62;
        }

        if (maxScore == null)
            return null;

        if (score <= maxScore.Value)
            return null;


        var newScore  = (float)(maxScore.Value - 1);

        var roleName  = RoleCategoryDisplayName(requiredRole);


        var gapSeverity = requiredYears == 1 ? "moderate" : "critical";
        var gapEntry  = $"{requiredYears}+ years as {roleName} ({gapSeverity})";

        var lines = reason.Split('\n');

        var existingGaps = lines
            .FirstOrDefault(l => l.StartsWith("Gaps:"))
            ?.Substring(5).Trim() ?? "none";


        var gapAlreadyPresent = existingGaps != "none"
            && existingGaps.Contains($"{requiredYears}+", StringComparison.OrdinalIgnoreCase);
        var newGaps = gapAlreadyPresent
            ? existingGaps
            : existingGaps == "none" ? gapEntry : $"{gapEntry}, {existingGaps}";

        var matched = lines
            .FirstOrDefault(l => l.StartsWith("Matched:"))
            ?.Substring(8).Trim() ?? "none";


        var verdict = forcedVerdict ?? RecomputeVerdict(newScore);

        var newReason = $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}";

        return (newScore, newReason);
    }


    public (float Score, string Reason)? TryApplyMultiCriticalCap(float score, string reason)
    {
        var gaps = reason.Split('\n')
            .FirstOrDefault(l => l.StartsWith("Gaps:"))
            ?.Substring(5).Trim() ?? string.Empty;


        var criticalCount = Regex.Matches(gaps, @"\(critical\)", RegexOptions.IgnoreCase).Count;
        if (criticalCount == 0) return null;

        var maxScore = criticalCount switch
        {
            1 => 64,
            2 => 57,
            3 => 50,
            _ => 44
        };

        if (score <= maxScore) return null;

        var lines   = reason.Split('\n');
        var matched = lines.FirstOrDefault(l => l.StartsWith("Matched:"))?.Substring(8).Trim() ?? "none";
        var verdict = RecomputeVerdict(maxScore);

        return ((float)maxScore,
            $"Verdict: {verdict}\nMatched: {matched}\nGaps: {gaps}");
    }


    public (bool CareerSwitcher, int TechnicalSkillsCount) ParseCareerSwitcherContext(string cvText)
    {
        if (string.IsNullOrWhiteSpace(cvText) || !cvText.TrimStart().StartsWith("{"))
            return (false, 0);

        try
        {
            using var doc = JsonDocument.Parse(cvText);
            var root = doc.RootElement;

            var careerSwitcher = root.TryGetProperty("career_switcher", out var cs)
                              && cs.ValueKind == JsonValueKind.True;

            var techSkillsCount = 0;
            if (root.TryGetProperty("technical_skills", out var ts) && ts.ValueKind == JsonValueKind.Array)
                techSkillsCount = ts.GetArrayLength();

            return (careerSwitcher, techSkillsCount);
        }
        catch
        {
            return (false, 0);
        }
    }


    public string[] ParseTargetRoles(string cvText)
    {
        if (string.IsNullOrWhiteSpace(cvText) || !cvText.TrimStart().StartsWith("{"))
            return Array.Empty<string>();

        try
        {
            using var doc = JsonDocument.Parse(cvText);
            if (!doc.RootElement.TryGetProperty("target_roles", out var arr) || arr.ValueKind != JsonValueKind.Array)
                return Array.Empty<string>();

            var result = new List<string>();
            foreach (var entry in arr.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.String)
                {
                    var s = entry.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) result.Add(s);
                }
            }
            return result.ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }


    public (float Score, string Reason)? TryApplyMismatchCap(
        float score, string reason, string jobTitle, string jobDescription, string[] candidateTargetRoles)
    {
        const int MismatchMaxScore = 24;
        if (score <= MismatchMaxScore) return null;


        var targetsPmFamily = candidateTargetRoles
            .Select(r => ClassifyRole(r ?? string.Empty))
            .Any(c => c == RoleCategory.PmPo || c == RoleCategory.Pmm);
        if (!targetsPmFamily) return null;

        var t = jobTitle.ToLowerInvariant();
        var d = jobDescription.ToLowerInvariant();


        var isMismatch =
            t.Contains("bonus manager")       || t.Contains("promo manager") ||
            t.Contains("liveops manager")     || t.Contains("live ops manager") ||
            t.Contains("smm manager")         || t.Contains("smm-manager") ||
            t.Contains("sourcing manager")    || t.Contains("procurement manager") ||
            t.Contains("presale manager")     || t.Contains("pre-sale manager") ||
            t.Contains("production operations manager") ||

            (t.Contains("growth") && t.Contains("operation") &&
             (d.Contains("anti-detect") || d.Contains("акаунт") || d.Contains("ban") || d.Contains("proxy"))) ||

            (d.Contains("fmcg") && (d.Contains("packag") || d.Contains("упаков") || d.Contains("виробни"))) ||
            (d.Contains("non-food") || d.Contains("non food") || d.Contains("серветк") ||
             d.Contains("упаков") && d.Contains("виробни")) ||
            (d.Contains("торгових марок") && d.Contains("виробни"));

        if (!isMismatch) return null;

        var newScore  = (float)MismatchMaxScore;
        var lines     = reason.Split('\n');
        var matched   = lines.FirstOrDefault(l => l.StartsWith("Matched:"))?.Substring(8).Trim() ?? "none";
        var verdict   = RecomputeVerdict(newScore);
        var newReason = $"Verdict: {verdict}\nMatched: {matched}\nGaps: core function mismatch — not a Product Management role (critical)";

        return (newScore, newReason);
    }


    public (float Score, string Reason)? TryApplyDomainLockCap(
        float score, string reason, string jobDescription)
    {
        const int DomainLockMaxScore = 44;
        if (score <= DomainLockMaxScore) return null;

        var d = jobDescription.ToLowerInvariant();


        var isEnergyLocked =
            d.Contains("ems") && (d.Contains("bess") || d.Contains("vpp") || d.Contains("smart grid")) ||
            d.Contains("bess") || d.Contains("vpp") ||
            d.Contains("energy trading") || d.Contains("power grid") ||
            d.Contains("scada") && d.Contains("energy") ||
            d.Contains("енергосистем") || d.Contains("батареї") && d.Contains("накопич");


        var isPharmaLocked =
            d.Contains("clinical trial") || d.Contains("regulatory affairs") ||
            d.Contains("drug lifecycle") || d.Contains("cns") && d.Contains("pharma") ||
            (d.Contains("фарм") && (d.Contains("реєстраці") || d.Contains("клінічн")));


        var isBankingLocked =
            d.Contains("nbu") || d.Contains("нбу") ||
            d.Contains("core banking") || d.Contains("swift") && d.Contains("aml") ||
            d.Contains("psd2") || d.Contains("банківськ") && d.Contains("ліценз");


        var isHardwareLocked =
            d.Contains("firmware") || d.Contains("fpga") ||
            d.Contains("embedded system") && d.Contains("product");

        if (!isEnergyLocked && !isPharmaLocked && !isBankingLocked && !isHardwareLocked)
            return null;

        var domain = isEnergyLocked  ? "energy systems (EMS/BESS/VPP)"
                   : isPharmaLocked  ? "pharma/MedTech regulatory"
                   : isBankingLocked ? "core banking/NBU regulation"
                   : "hardware/embedded systems";

        var lines   = reason.Split('\n');
        var matched = lines.FirstOrDefault(l => l.StartsWith("Matched:"))?.Substring(8).Trim() ?? "none";
        var gaps    = lines.FirstOrDefault(l => l.StartsWith("Gaps:"))?.Substring(5).Trim() ?? "none";
        var newGaps = $"deep {domain} domain knowledge (critical — non-transferable), {gaps}";
        var verdict = RecomputeVerdict(DomainLockMaxScore);

        return ((float)DomainLockMaxScore,
            $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
    }


    public (float Score, string Reason)? TryApplyPlatformToolCap(
        float score, string reason, string jobTitle, string jobDescription)
    {
        const int PlatformToolMaxScore = 52;
        if (score <= PlatformToolMaxScore) return null;

        var t = jobTitle.ToLowerInvariant();
        var d = jobDescription.ToLowerInvariant();


        var isForgiving =
            d.Contains("eager to learn") || d.Contains("willing to learn") ||
            d.Contains("готові навчати") || d.Contains("або готовність") ||
            d.Contains("will train") || d.Contains("навчимо") ||
            d.Contains("open to candidates without");
        if (isForgiving) return null;


        var isAmazonLocked =
            (t.Contains("amazon") || d.Contains("amazon seller central") ||
             d.Contains("amazon seo") || d.Contains("helium 10") ||
             d.Contains("amazon brand analytics") || d.Contains("amazon ppc"))
            && !t.Contains("aws") && !t.Contains("cloud");


        var isPpcLocked =
            (d.Contains("google ads manager") || d.Contains("google adwords")) &&
            (d.Contains("campaign management") || d.Contains("roas") || d.Contains("cpa"));

        if (!isAmazonLocked && !isPpcLocked) return null;

        var platform = isAmazonLocked ? "Amazon marketplace (Seller Central, Amazon SEO)"
                                      : "Google Ads hands-on campaign management";

        var lines   = reason.Split('\n');
        var matched = lines.FirstOrDefault(l => l.StartsWith("Matched:"))?.Substring(8).Trim() ?? "none";
        var gaps    = lines.FirstOrDefault(l => l.StartsWith("Gaps:"))?.Substring(5).Trim() ?? "none";
        var gapEntry = $"{platform} expertise (critical — platform-specific, not in CV)";
        var gapAlready = gaps.Contains("Amazon") || gaps.Contains("Seller Central");
        var newGaps = gapAlready ? gaps : $"{gapEntry}, {gaps}";
        var verdict = RecomputeVerdict(PlatformToolMaxScore);

        return ((float)PlatformToolMaxScore,
            $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
    }


    private static RoleCategory ClassifyRole(string title)
    {
        var t = title.ToLowerInvariant();

        if (t.Contains("product marketing"))       return RoleCategory.Pmm;
        if (t.Contains("growth marketing"))        return RoleCategory.Pmm;
        if (t.Contains("growth manager"))          return RoleCategory.Pmm;
        if (t.Contains("project manager"))         return RoleCategory.ProjectManager;
        if (t.Contains("program manager"))         return RoleCategory.ProjectManager;
        if (t.Contains("product manager"))         return RoleCategory.PmPo;
        if (t.Contains("product owner"))           return RoleCategory.PmPo;
        if (t.Contains("head of product"))         return RoleCategory.PmPo;
        if (t.Contains("chief product officer"))   return RoleCategory.PmPo;
        if (t.StartsWith("cpo ") || t == "cpo")   return RoleCategory.PmPo;
        if (t.Contains("product lead"))            return RoleCategory.PmPo;

        if (t == "product" || t.StartsWith("product (") || t.StartsWith("product —"))
                                                   return RoleCategory.PmPo;
        if (t.Contains("business analyst"))        return RoleCategory.BusinessAnalyst;
        if (t.Contains("systems analyst") ||
            t.Contains("system analyst"))          return RoleCategory.BusinessAnalyst;
        if (t.Contains("data analyst"))            return RoleCategory.DataAnalyst;
        if (t.Contains("data scientist"))          return RoleCategory.DataAnalyst;
        if (t.Contains("bi analyst") ||
            t.Contains("business intelligence analyst")) return RoleCategory.DataAnalyst;
        if (t.Contains("portfolio manager"))       return RoleCategory.PmPo;
        if (t.Contains("developer") ||
            t.Contains("engineer") ||
            t.Contains("software") ||
            t.Contains("backend") ||
            t.Contains("frontend") ||
            t.Contains("fullstack"))               return RoleCategory.Developer;
        if (t.Contains("designer") ||
            t.Contains("ux") || t.Contains("ui")) return RoleCategory.Designer;
        if (t.Contains("marketing"))               return RoleCategory.Marketing;

        return RoleCategory.Other;
    }


    private static int ExtractMinRequiredYears(string description)
    {
        if (string.IsNullOrWhiteSpace(description))
            return 0;


        var yearPatterns = new[]
        {

            @"виключно\s+від\s+(\d+)\s+рок",
            @"досвіду?\s+від\s+(\d+)\s+рок",
            @"досвід(?:\s+роботи)?\s+від\s+(\d+)\s+рок",
            @"мінімум\s+(\d+)\s+рок",
            @"не\s+менше\s+(\d+)\s+рок",
            @"від\s+(\d+)\s+рок",
            @"(\d+)\+\s*рок",
            @"(\d+)[–-](\d+)\s+рок",

            @"(\d+)\s+years?\s+of\s+(?:product\s+)?(?:management|experience)",
            @"at\s+least\s+(\d+)\s+years?",
            @"minimum\s+(\d+)\s+years?",
            @"(\d+)\+\s+years?",
            @"(\d+)-(\d+)\s+years?",
        };


        var monthPatterns = new[]
        {

            @"досвіду?\s+від\s+(\d+)\s+місяц",
            @"від\s+(\d+)\s+місяц",
            @"мінімум\s+(\d+)\s+місяц",
            @"не\s+менше\s+(\d+)\s+місяц",
            @"(\d+)\+\s*місяц",

            @"(\d+)\s+months?\s+of\s+(?:product\s+)?(?:management|experience)",
            @"at\s+least\s+(\d+)\s+months?",
            @"minimum\s+(\d+)\s+months?",
            @"(\d+)\+\s+months?",
        };

        var counts = new Dictionary<int, int>();


        foreach (var pattern in yearPatterns)
        {
            foreach (Match match in Regex.Matches(description, pattern, RegexOptions.IgnoreCase))
            {
                if (int.TryParse(match.Groups[1].Value, out var years) && years > 0 && years <= 20)
                    counts[years] = counts.GetValueOrDefault(years) + 1;
            }
        }


        foreach (var pattern in monthPatterns)
        {
            foreach (Match match in Regex.Matches(description, pattern, RegexOptions.IgnoreCase))
            {
                if (!int.TryParse(match.Groups[1].Value, out var months) || months <= 0 || months > 240)
                    continue;

                var yearsEquiv = MonthsToYearsEquivalent(months);
                if (yearsEquiv > 0)
                    counts[yearsEquiv] = counts.GetValueOrDefault(yearsEquiv) + 1;
            }
        }

        if (counts.Count == 0) return 0;


        var maxCount = counts.Values.Max();
        return counts
            .Where(kv => kv.Value == maxCount)
            .Select(kv => kv.Key)
            .Max();
    }


    private static int MonthsToYearsEquivalent(int months) =>
        months < 6  ? 0 :
        months < 18 ? 1 :
        months < 30 ? 2 :
        months < 42 ? 3 :
        months < 54 ? 4 : 5;


    internal static string RecomputeVerdict(float score) =>
        score >= 85 ? "strong_fit"
      : score >= 65 ? "good_fit"
      : score >= 35 ? "partial_fit"
      :               "weak_fit";


    internal static string RewriteVerdictInReason(string reason, float score)
    {
        var newVerdict = RecomputeVerdict(score);
        if (string.IsNullOrEmpty(reason))
            return $"Verdict: {newVerdict}\nMatched: none\nGaps: none";

        var lines = reason.Split('\n');
        var hasVerdict = false;
        for (var i = 0; i < lines.Length; i++)
        {
            if (lines[i].StartsWith("Verdict:"))
            {
                lines[i] = $"Verdict: {newVerdict}";
                hasVerdict = true;
                break;
            }
        }

        return hasVerdict
            ? string.Join('\n', lines)
            : $"Verdict: {newVerdict}\n{reason}";
    }

    private static double GetCandidateYears(RoleWeightedYears r, RoleCategory category) =>
        category switch
        {
            RoleCategory.PmPo            => r.PmPo,
            RoleCategory.Pmm             => r.Pmm,
            RoleCategory.BusinessAnalyst => r.BusinessAnalyst,
            RoleCategory.ProjectManager  => r.ProjectManager,
            RoleCategory.Developer       => r.Developer,
            RoleCategory.DataAnalyst     => r.DataAnalyst,
            RoleCategory.Designer        => r.Designer,
            RoleCategory.Marketing       => r.Marketing,
            _                            => 0
        };


    private static double GetEffectiveCandidateYears(
        RoleWeightedYears r, RoleCategory targetRole,
        bool careerSwitcher = false, int technicalSkillsCount = 0)
    {
        var direct = GetCandidateYears(r, targetRole);

        var (multiplier, maxBonus) = targetRole switch
        {
            RoleCategory.PmPo            => (0.30, 2.0),
            RoleCategory.Pmm             => (0.30, 2.0),
            RoleCategory.ProjectManager  => (0.25, 1.5),
            RoleCategory.BusinessAnalyst => (0.40, 2.0),
            RoleCategory.DataAnalyst     => (0.20, 1.0),
            _                            => (0.0, 0.0)
        };


        var devBonus = 0.0;
        if (multiplier > 0.0 && r.Developer > 0)
            devBonus = Math.Min(r.Developer * multiplier, maxBonus);


        var careerSwitcherTarget = targetRole is RoleCategory.PmPo
                                              or RoleCategory.Pmm
                                              or RoleCategory.BusinessAnalyst;
        if (careerSwitcher
            && careerSwitcherTarget
            && r.Developer < 1.0
            && technicalSkillsCount >= 5
            && devBonus < 1.0)
        {
            devBonus = 1.0;
        }

        return direct + devBonus;
    }

    private static string RoleCategoryDisplayName(RoleCategory category) =>
        category switch
        {
            RoleCategory.PmPo            => "Product Manager",
            RoleCategory.Pmm             => "Product Marketing Manager",
            RoleCategory.BusinessAnalyst => "Business Analyst",
            RoleCategory.ProjectManager  => "Project Manager",
            RoleCategory.Developer       => "Developer/Engineer",
            RoleCategory.DataAnalyst     => "Data Analyst",
            RoleCategory.Designer        => "Designer",
            RoleCategory.Marketing       => "Marketing Manager",
            _                            => "required role"
        };


    internal enum RoleCategory
    {
        PmPo, Pmm, BusinessAnalyst, ProjectManager,
        Developer, DataAnalyst, Designer, Marketing, Other
    }
}
