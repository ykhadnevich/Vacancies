using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.FamilyCaps;


public sealed class PmFamilyCaps : IFamilyCaps
{
    private const int MismatchMaxScore     = 24;
    private const int DomainLockMaxScore   = 44;
    private const int PlatformToolMaxScore = 52;


    public (float Score, string Reason)? TryApplyMismatchCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string[] candidateTargetRoles,
        string cvText)
    {
        if (score <= MismatchMaxScore) return null;

        var targetsPmFamily = candidateTargetRoles
            .Any(r =>
            {
                var rr = (r ?? string.Empty).ToLowerInvariant();
                return rr.Contains("product manager")
                    || rr.Contains("product owner")
                    || rr.Contains("head of product")
                    || rr.Contains("product marketing")
                    || rr.Contains("growth manager");
            });
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
             (d.Contains("упаков") && d.Contains("виробни"))) ||
            (d.Contains("торгових марок") && d.Contains("виробни"));

        if (!isMismatch) return null;

        var matched = ExtractField(reason, "Matched:") ?? "none";
        var verdict = RecomputeVerdict(MismatchMaxScore);
        var newReason =
            $"Verdict: {verdict}\n" +
            $"Matched: {matched}\n" +
            $"Gaps: core function mismatch — not a Product Management role (critical)";

        return (MismatchMaxScore, newReason);
    }


    public (float Score, string Reason)? TryApplyDomainLockCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string cvText)
    {
        if (score <= DomainLockMaxScore) return null;

        var d = jobDescription.ToLowerInvariant();

        var isEnergyLocked =
            (d.Contains("ems") && (d.Contains("bess") || d.Contains("vpp") || d.Contains("smart grid"))) ||
            d.Contains("bess") || d.Contains("vpp") ||
            d.Contains("energy trading") || d.Contains("power grid") ||
            (d.Contains("scada") && d.Contains("energy")) ||
            d.Contains("енергосистем") || (d.Contains("батареї") && d.Contains("накопич"));

        var isPharmaLocked =
            d.Contains("clinical trial") || d.Contains("regulatory affairs") ||
            d.Contains("drug lifecycle") || (d.Contains("cns") && d.Contains("pharma")) ||
            (d.Contains("фарм") && (d.Contains("реєстраці") || d.Contains("клінічн")));

        var isBankingLocked =
            d.Contains("nbu") || d.Contains("нбу") ||
            d.Contains("core banking") || (d.Contains("swift") && d.Contains("aml")) ||
            d.Contains("psd2") || (d.Contains("банківськ") && d.Contains("ліценз"));

        var isHardwareLocked =
            d.Contains("firmware") || d.Contains("fpga") ||
            (d.Contains("embedded system") && d.Contains("product"));

        if (!isEnergyLocked && !isPharmaLocked && !isBankingLocked && !isHardwareLocked)
            return null;

        var domain =
            isEnergyLocked  ? "energy systems (EMS/BESS/VPP)" :
            isPharmaLocked  ? "pharma/MedTech regulatory" :
            isBankingLocked ? "core banking/NBU regulation" :
                              "hardware/embedded systems";

        var matched = ExtractField(reason, "Matched:") ?? "none";
        var gaps    = ExtractField(reason, "Gaps:")    ?? "none";
        var newGaps = $"deep {domain} domain knowledge (critical — non-transferable), {gaps}";
        var verdict = RecomputeVerdict(DomainLockMaxScore);

        return (DomainLockMaxScore,
                $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
    }


    public (float Score, string Reason)? TryApplyPlatformToolCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string cvText)
    {
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

        var platform = isAmazonLocked
            ? "Amazon marketplace (Seller Central, Amazon SEO)"
            : "Google Ads hands-on campaign management";

        var matched  = ExtractField(reason, "Matched:") ?? "none";
        var gaps     = ExtractField(reason, "Gaps:")    ?? "none";
        var gapEntry = $"{platform} expertise (critical — platform-specific, not in CV)";
        var gapAlready =
            gaps.Contains("Amazon", StringComparison.OrdinalIgnoreCase) ||
            gaps.Contains("Seller Central", StringComparison.OrdinalIgnoreCase);
        var newGaps = gapAlready ? gaps : $"{gapEntry}, {gaps}";
        var verdict = RecomputeVerdict(PlatformToolMaxScore);

        return (PlatformToolMaxScore,
                $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
    }


    private static string? ExtractField(string reason, string prefix)
    {
        if (string.IsNullOrEmpty(reason)) return null;
        var line = reason.Split('\n').FirstOrDefault(l => l.StartsWith(prefix));
        return line?.Substring(prefix.Length).Trim();
    }

    private static string RecomputeVerdict(float score) => score switch
    {
        >= 85 => "strong_fit",
        >= 65 => "good_fit",
        >= 35 => "partial_fit",
        _     => "weak_fit"
    };
}
