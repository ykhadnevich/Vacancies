using System.Text.Json;
using Domain.Enums;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;


/// <summary>
/// Deterministic post-processing safety nets for Monolithic scoring.
///
/// The LLM is generally good at semantic understanding but has documented
/// blind spots — most notably seniority gaps (it inflates skill match) and
/// cross-stack mismatches inside the same broad "developer" family.
///
/// These guardrails are intentionally conservative: they only trigger on
/// HARD, unambiguous evidence (e.g. junior CV on senior vacancy with explicit
/// fields), and they CAP sub-scores rather than reset them. The composite is
/// still computed downstream from the (possibly capped) sub-scores.
/// </summary>
public static class MonolithicGuardrails
{

    /// <summary>
    /// Hard cap when the candidate is materially under-qualified for the role.
    /// </summary>
    public const double UnderQualifiedSeniorityCap = 0.2;


    /// <summary>
    /// Hard cap when the candidate's tech sub-stack does not match the
    /// vacancy's tech sub-stack (e.g. Frontend → Backend ERP).
    /// </summary>
    public const double CrossStackSkillCap = 0.3;


    /// <summary>
    /// Hard cap on role_intent_match when the vacancy and CV are in different
    /// engineering sub-stacks.
    /// </summary>
    public const double CrossStackRoleIntentCap = 0.2;


    public sealed record GuardrailReport(
        bool UnderQualifiedTriggered,
        bool CrossStackTriggered,
        string? Reason);


    public static (SubScores capped, GuardrailReport report) Apply(
        SubScores raw,
        JsonElement cvSummary,
        JsonElement vacancyAnalysis,
        string? vacancyRawText = null)
    {
        var reasons = new List<string>();

        var skill      = raw.SkillMatch;
        var seniority  = raw.SeniorityMatch;
        var roleIntent = raw.RoleIntentMatch;

        bool underQualified = IsUnderQualifiedHard(cvSummary, vacancyAnalysis, vacancyRawText, out var seniorityReason);
        if (underQualified)
        {
            seniority = Math.Min(seniority, UnderQualifiedSeniorityCap);
            reasons.Add(seniorityReason);
        }


        // CROSS-STACK GUARDRAIL DISABLED — measured net-zero NDCG@10 impact
        // (5 CV improved, 5 CV regressed). The simple keyword-based detector
        // doesn't capture real overlaps:
        //   * Backend ↔ DevOps (Docker/K8s/Terraform)
        //   * iOS ↔ React Native (both Mobile)
        //   * Frontend ↔ Frontend across frameworks (React vs Angular)
        // Re-enable only with a more nuanced (e.g. embedding-based) detector.
        bool crossStack = false;

        var capped = raw with
        {
            SkillMatch      = skill,
            SeniorityMatch  = seniority,
            RoleIntentMatch = roleIntent,
        };

        return (capped, new GuardrailReport(
            UnderQualifiedTriggered: underQualified,
            CrossStackTriggered:     crossStack,
            Reason:                  reasons.Count > 0 ? string.Join("; ", reasons) : null));
    }


    /// <summary>
    /// CV is junior-or-less and vacancy explicitly requires senior-or-more.
    /// Only fires on clear gap of 2+ levels — Middle CV on Senior is allowed
    /// (the +1 hire is a normal pattern).
    /// </summary>
    public static bool IsUnderQualifiedHard(
        JsonElement cv,
        JsonElement vacancy,
        string? vacancyRawText,
        out string reason)
    {
        reason = string.Empty;

        var cvLevel = ReadSeniority(cv, "seniority");
        var vacLevel = DeriveVacancySeniority(vacancy);


        if (vacLevel == SeniorityLevel.NotSpecified && !string.IsNullOrWhiteSpace(vacancyRawText))
            vacLevel = SniffSeniorityFromText(vacancyRawText);

        if (cvLevel == SeniorityLevel.NotSpecified || vacLevel == SeniorityLevel.NotSpecified)
            return false;

        int cvRank = SeniorityRank(cvLevel);
        int vacRank = SeniorityRank(vacLevel);
        int gap = vacRank - cvRank;

        if (gap >= 2)
        {
            reason = $"under_qualified: CV={cvLevel}, vacancy={vacLevel}, gap={gap}";
            return true;
        }
        return false;
    }


    /// <summary>
    /// Both sides identify as some flavour of "developer/engineer" but the
    /// actual tech sub-stacks are disjoint. Detected via target_roles vs
    /// role_title keyword groups.
    /// </summary>
    public static bool IsCrossStackHard(
        JsonElement cv,
        JsonElement vacancy,
        string? vacancyRawText,
        out string reason)
    {
        reason = string.Empty;

        var cvStack  = DetectCvStack(cv);
        var vacStack = DetectVacancyStack(vacancy);


        if (vacStack == TechStack.Unknown && !string.IsNullOrWhiteSpace(vacancyRawText))
            vacStack = ClassifyStack(FirstLineOrTitle(vacancyRawText));

        if (cvStack == TechStack.Unknown || vacStack == TechStack.Unknown)
            return false;


        if (cvStack == TechStack.Fullstack || vacStack == TechStack.Fullstack)
            return false;


        if (cvStack != vacStack)
        {
            reason = $"cross_stack: CV={cvStack}, vacancy={vacStack}";
            return true;
        }
        return false;
    }


    private static string FirstLineOrTitle(string rawText)
    {

        if (string.IsNullOrEmpty(rawText)) return string.Empty;
        var nl = rawText.IndexOf('\n');
        return nl > 0 ? rawText[..nl] : rawText[..Math.Min(120, rawText.Length)];
    }


    private static SeniorityLevel SniffSeniorityFromText(string rawText)
    {
        var t = rawText.ToLowerInvariant();


        if (t.Contains("trainee") || t.Contains("intern ") || t.Contains("стажер"))
            return SeniorityLevel.Internship;
        if (t.Contains("lead ") || t.Contains("principal") || t.Contains("staff engineer")
            || t.Contains("tech lead") || t.Contains("head of"))
            return SeniorityLevel.Lead;
        if (t.Contains("senior ") || t.Contains("senior+") || t.Contains("сеньйор"))
            return SeniorityLevel.Senior;
        if (t.Contains("middle") || t.Contains("mid ") || t.Contains("mid-level"))
            return SeniorityLevel.Middle;
        if (t.Contains("junior ") || t.Contains("джуніор") || t.Contains("молодший"))
            return SeniorityLevel.Junior;

        return SeniorityLevel.NotSpecified;
    }


    private enum TechStack
    {
        Unknown,
        Frontend,
        Backend,
        Fullstack,
        Mobile,
        DevOps,
        Data,
        Qa,
        Ml,
        EmbeddedSystems,
    }


    private static TechStack DetectCvStack(JsonElement cv)
    {
        if (cv.ValueKind != JsonValueKind.Object) return TechStack.Unknown;


        if (cv.TryGetProperty("target_roles", out var tr) && tr.ValueKind == JsonValueKind.Array)
        {
            foreach (var role in tr.EnumerateArray())
            {
                if (role.ValueKind != JsonValueKind.String) continue;
                var stack = ClassifyStack(role.GetString());
                if (stack != TechStack.Unknown) return stack;
            }
        }


        if (cv.TryGetProperty("experience", out var exp) && exp.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in exp.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                foreach (var field in new[] { "role", "title", "position" })
                {
                    if (!item.TryGetProperty(field, out var v) || v.ValueKind != JsonValueKind.String) continue;
                    var stack = ClassifyStack(v.GetString());
                    if (stack != TechStack.Unknown) return stack;
                }
            }
        }

        return TechStack.Unknown;
    }


    private static TechStack DetectVacancyStack(JsonElement vacancy)
    {
        if (vacancy.ValueKind != JsonValueKind.Object) return TechStack.Unknown;

        if (vacancy.TryGetProperty("role_title", out var rt))
        {
            if (rt.ValueKind == JsonValueKind.String)
            {
                var s = ClassifyStack(rt.GetString());
                if (s != TechStack.Unknown) return s;
            }
            if (rt.ValueKind == JsonValueKind.Object
                && rt.TryGetProperty("en", out var en)
                && en.ValueKind == JsonValueKind.String)
            {
                var s = ClassifyStack(en.GetString());
                if (s != TechStack.Unknown) return s;
            }
        }

        return TechStack.Unknown;
    }


    private static TechStack ClassifyStack(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) return TechStack.Unknown;
        var t = title.ToLowerInvariant();


        if (t.Contains("fullstack") || t.Contains("full-stack") || t.Contains("full stack")
            || t.Contains("повний стек"))
            return TechStack.Fullstack;

        if (t.Contains("frontend") || t.Contains("front-end") || t.Contains("front end")
            || t.Contains("react developer") || t.Contains("vue developer") || t.Contains("angular developer")
            || t.Contains("ui engineer") || t.Contains("ui developer")
            || t.Contains("фронтенд"))
            return TechStack.Frontend;

        if (t.Contains("backend") || t.Contains("back-end") || t.Contains("back end")
            || t.Contains(".net developer") || t.Contains("java developer") || t.Contains("python developer")
            || t.Contains("go developer") || t.Contains("php developer") || t.Contains("ruby developer")
            || t.Contains("bff") || t.Contains("erp")
            || t.Contains("бекенд"))
            return TechStack.Backend;

        if (t.Contains("devops") || t.Contains("sre") || t.Contains("site reliability")
            || t.Contains("platform engineer") || t.Contains("infrastructure"))
            return TechStack.DevOps;

        if (t.Contains("data engineer") || t.Contains("data analyst") || t.Contains("data scientist")
            || t.Contains("analytics engineer") || t.Contains("bi engineer")
            || t.Contains("аналітик даних") || t.Contains("дата-аналітик"))
            return TechStack.Data;

        if (t.Contains("ml engineer") || t.Contains("machine learning")
            || t.Contains("ai engineer") || t.Contains("mlops"))
            return TechStack.Ml;

        if (t.Contains("ios ") || t.Contains("android ") || t.Contains("mobile developer")
            || t.Contains("react native") || t.Contains("flutter"))
            return TechStack.Mobile;

        if (t.Contains("qa engineer") || t.Contains("qa automation") || t.Contains("sdet")
            || t.Contains("test engineer") || t.Contains("quality assurance")
            || t.Contains("тестувальник"))
            return TechStack.Qa;

        if (t.Contains("embedded") || t.Contains("firmware"))
            return TechStack.EmbeddedSystems;

        return TechStack.Unknown;
    }


    private static SeniorityLevel ReadSeniority(JsonElement obj, string field)
    {
        if (obj.ValueKind != JsonValueKind.Object) return SeniorityLevel.NotSpecified;
        if (!obj.TryGetProperty(field, out var v) || v.ValueKind != JsonValueKind.String)
            return SeniorityLevel.NotSpecified;
        return SeniorityBoundaries.FromString(v.GetString());
    }


    private static SeniorityLevel DeriveVacancySeniority(JsonElement vacancy)
    {
        if (vacancy.ValueKind != JsonValueKind.Object) return SeniorityLevel.NotSpecified;


        if (vacancy.TryGetProperty("seniority_required", out var sr)
            && sr.ValueKind == JsonValueKind.String)
        {
            var fromString = SeniorityBoundaries.FromString(sr.GetString());
            if (fromString != SeniorityLevel.NotSpecified) return fromString;
        }


        if (vacancy.TryGetProperty("min_years_experience", out var yEl)
            && yEl.ValueKind == JsonValueKind.Number)
        {
            int years = (int)Math.Round(yEl.GetDouble());
            if (years > 0) return SeniorityBoundaries.FromYears(years);
        }

        return SeniorityLevel.NotSpecified;
    }


    private static int SeniorityRank(SeniorityLevel level) => level switch
    {
        SeniorityLevel.Internship   => 0,
        SeniorityLevel.Junior       => 1,
        SeniorityLevel.Middle       => 2,
        SeniorityLevel.Senior       => 3,
        SeniorityLevel.Lead         => 4,
        _                           => -1,
    };
}
