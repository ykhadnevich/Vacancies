using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.FamilyCaps;


public sealed class EngFamilyCaps : IFamilyCaps
{
    private const int MismatchMaxScore     = 24;
    private const int DomainLockMaxScore   = 44;
    private const int PlatformToolMaxScore = 52;


    private static readonly (string Domain, string[] JobSignals, string[] CvSignals)[] EngDomainLocks =
    {
        ("Pharma/MedTech",
            JobSignals: new[] { "hl7", "fhir", "gxp", "21 cfr part 11", "clinical trial",
                                "regulatory affairs", "medical device" },
            CvSignals:  new[] { "hl7", "fhir", "gxp", "21 cfr", "pharma", "medtech", "clinical" }),

        ("Banking/Payments",
            JobSignals: new[] { "pci-dss", "pci dss", "iso 8583", "emv", "fix protocol",
                                "core banking", "swift" },
            CvSignals:  new[] { "pci-dss", "pci dss", "iso 8583", "emv", "fix protocol",
                                "banking", "fintech", "payment" }),

        ("Energy/SCADA",
            JobSignals: new[] { "modbus", "dnp3", "iec 61850", "scada", "energy grid", "bess", "vpp" },
            CvSignals:  new[] { "modbus", "dnp3", "iec 61850", "scada", "energy", "power", "grid" }),

        ("Telco",
            JobSignals: new[] { "ss7", "diameter", "oss/bss", "oss bss" },
            CvSignals:  new[] { "ss7", "diameter", "oss", "bss", "telco", "telecom" }),
    };

    public (float Score, string Reason)? TryApplyDomainLockCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string cvText)
    {
        if (score <= DomainLockMaxScore) return null;

        var d = jobDescription.ToLowerInvariant();
        var cv = cvText.ToLowerInvariant();

        foreach (var lockEntry in EngDomainLocks)
        {
            var jobHasDomain = lockEntry.JobSignals.Any(s => d.Contains(s));
            if (!jobHasDomain) continue;

            var cvHasDomain = lockEntry.CvSignals.Any(s => cv.Contains(s));
            if (cvHasDomain) continue;

            var matched = ExtractField(reason, "Matched:") ?? "none";
            var gaps    = ExtractField(reason, "Gaps:")    ?? "none";
            var newGaps = $"{lockEntry.Domain} domain knowledge — no CV signal (critical), {gaps}";
            var verdict = RecomputeVerdict(DomainLockMaxScore);
            return (DomainLockMaxScore,
                    $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
        }

        return null;
    }

    public (float Score, string Reason)? TryApplyPlatformToolCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string cvText)
    {
        if (score <= PlatformToolMaxScore) return null;

        var d = jobDescription.ToLowerInvariant();
        var cv = cvText.ToLowerInvariant();

        var isForgiving =
            d.Contains("eager to learn") || d.Contains("willing to learn") ||
            d.Contains("готові навчати") || d.Contains("навчимо") ||
            d.Contains("open to candidates without");
        if (isForgiving) return null;


        var jobRequiresK8sOperator =
            d.Contains("kubernetes operator") ||
            (d.Contains("kubernetes") && (d.Contains("production") || d.Contains("3+ years") ||
                                          d.Contains("3 years")    || d.Contains("5+ years")));
        var cvHasK8s = cv.Contains("kubernetes") || cv.Contains("k8s");

        if (jobRequiresK8sOperator && !cvHasK8s)
        {
            var matched = ExtractField(reason, "Matched:") ?? "none";
            var gaps    = ExtractField(reason, "Gaps:")    ?? "none";
            var newGaps = $"production Kubernetes operator experience (critical), {gaps}";
            var verdict = RecomputeVerdict(PlatformToolMaxScore);
            return (PlatformToolMaxScore,
                    $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
        }


        var awsSpecific = new[] { "amazon eks", "aws lambda hands-on", "lambda production",
                                  "amazon ecs production", "aws step functions production" };
        var jobRequiresAws = awsSpecific.Any(s => d.Contains(s));
        var cvHasAws = cv.Contains("eks") || cv.Contains("lambda") || cv.Contains("ecs") ||
                       cv.Contains("aws");

        if (jobRequiresAws && !cvHasAws)
        {
            var matched = ExtractField(reason, "Matched:") ?? "none";
            var gaps    = ExtractField(reason, "Gaps:")    ?? "none";
            var newGaps = $"AWS-specific service experience (critical), {gaps}";
            var verdict = RecomputeVerdict(PlatformToolMaxScore);
            return (PlatformToolMaxScore,
                    $"Verdict: {verdict}\nMatched: {matched}\nGaps: {newGaps}");
        }

        return null;
    }

    public (float Score, string Reason)? TryApplyMismatchCap(
        float score,
        string reason,
        string jobTitle,
        string jobDescription,
        string[] candidateTargetRoles,
        string cvText)
    {
        if (score <= MismatchMaxScore) return null;


        var targetsIcEngineer = candidateTargetRoles.Any(r =>
        {
            var rr = (r ?? string.Empty).ToLowerInvariant();
            var isEngineer = rr.Contains("backend")  || rr.Contains("frontend") ||
                             rr.Contains("fullstack") || rr.Contains("mobile") ||
                             rr.Contains("devops")   || rr.Contains("software engineer") ||
                             rr.Contains("software developer") || rr.Contains("ml engineer");
            var isLead = rr.Contains("engineering manager") || rr.Contains("em ") ||
                         rr.Contains("tech lead") || rr.Contains("team lead");
            return isEngineer && !isLead;
        });
        if (!targetsIcEngineer) return null;

        var t = jobTitle.ToLowerInvariant();

        var isMismatch =
            t.Contains("engineering manager") || t.Contains("em ") ||
            t.Contains("sales engineer")      || t.Contains("solutions architect") ||
            t.Contains("pre-sales architect") || t.Contains("presale architect") ||
            t.Contains("manual qa")           || t.Contains("qa manual") ||
            t.Contains("database administrator") || t.Contains("dba") ||
            t.Contains("it support")          || t.Contains("helpdesk");

        if (!isMismatch) return null;

        var matched = ExtractField(reason, "Matched:") ?? "none";
        var verdict = RecomputeVerdict(MismatchMaxScore);
        var newReason =
            $"Verdict: {verdict}\n" +
            $"Matched: {matched}\n" +
            $"Gaps: core function mismatch — vacancy is not an IC engineering role (critical)";
        return (MismatchMaxScore, newReason);
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
