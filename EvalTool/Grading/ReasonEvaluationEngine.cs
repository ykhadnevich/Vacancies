using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvalTool.Grading;


public sealed class ReasonEvaluationEngine
{
    private readonly IFactualityCheckService _factuality;
    private readonly ReasonClaimExtractor _extractor;
    private readonly ILogger<ReasonEvaluationEngine> _logger;

    public ReasonEvaluationEngine(
        IFactualityCheckService factuality,
        ReasonClaimExtractor extractor,
        ILogger<ReasonEvaluationEngine> logger)
    {
        _factuality = factuality;
        _extractor = extractor;
        _logger = logger;
    }


    public async Task<CaseScores> GradeAsync(
        string caseId,
        string scoringResultJson,
        string cvSummaryJson,
        string vacancyAnalysisJson,
        CancellationToken ct = default)
    {
        var scoring = JsonDocument.Parse(scoringResultJson).RootElement;
        var reasonEn = scoring.TryGetProperty("reason_en", out var reEn) ? reEn.GetString() ?? "" : "";
        var reasonUk = scoring.TryGetProperty("reason_uk", out var reUk) ? reUk.GetString() ?? "" : "";
        var score = scoring.TryGetProperty("score", out var sc) ? sc.GetDouble() : 0.0;
        var evidence = scoring.TryGetProperty("evidence", out var ev) ? ev : default;

        var missingMustHaves = ReadStringArray(evidence, "missing_must_haves");
        var matchedSkills = ReadStringArray(evidence, "matched_skills");
        var antiFlags = ReadStringArray(evidence, "triggered_anti_flags");

        var scores = new Dictionary<string, double>();


        if (!string.IsNullOrWhiteSpace(reasonEn))
        {
            var claims = _extractor.Extract(reasonEn);
            if (claims.Count == 0)
            {


                scores["reason.factuality_supported_pct"] = 1.0;
                scores["reason.factuality_min_score"] = 1.0;
                scores["reason.factuality_claims_extracted"] = 0.0;
            }
            else
            {
                var document = BuildPremise(cvSummaryJson, vacancyAnalysisJson, evidence);
                IReadOnlyList<FactualityVerdict> verdicts;
                try
                {
                    verdicts = await _factuality.CheckAsync(document, claims, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Factuality service threw for case {CaseId} — defaulting to unsupported",
                        caseId);
                    verdicts = claims.Select(c => new FactualityVerdict(c, false, 0.0)).ToList();
                }

                var supportedCount = verdicts.Count(v => v.IsSupported);
                scores["reason.factuality_supported_pct"] =
                    verdicts.Count == 0 ? 1.0 : (double)supportedCount / verdicts.Count;
                scores["reason.factuality_min_score"] =
                    verdicts.Count == 0 ? 1.0 : verdicts.Min(v => v.Confidence);
                scores["reason.factuality_claims_extracted"] = claims.Count;
            }
        }
        else
        {


            scores["reason.factuality_supported_pct"] = 0.0;
            scores["reason.factuality_min_score"] = 0.0;
            scores["reason.factuality_claims_extracted"] = 0.0;
        }


        scores["reason.calibration"] = CalibrationOk(reasonEn, score) ? 1.0 : 0.0;
        scores["reason.hallucination_strict"] = NoStrictHallucination(reasonEn, missingMustHaves, antiFlags) ? 1.0 : 0.0;
        scores["reason.bilingual_balanced"] = BilingualBalanced(reasonEn, reasonUk) ? 1.0 : 0.0;
        scores["reason.length_ok"] = LengthOk(reasonEn) ? 1.0 : 0.0;
        scores["reason.format_ok"] = FormatOk(reasonEn) ? 1.0 : 0.0;
        scores["reason.context_lead_present"] = HasContextLead(reasonEn) ? 1.0 : 0.0;
        scores["reason.anti_flag_surfaced"] = AntiFlagSurfaced(reasonEn, antiFlags) ? 1.0 : 0.0;


        var aggregateKeys = scores.Keys.Where(k => k != "reason.factuality_claims_extracted").ToList();
        var overall = aggregateKeys.Count == 0 ? 0.0 : aggregateKeys.Sum(k => scores[k]) / aggregateKeys.Count;

        return new CaseScores(
            CaseId: caseId,
            FieldScores: scores,
            Overall: overall);
    }


    private static string BuildPremise(string cvSummaryJson, string vacancyAnalysisJson, JsonElement evidence)
    {


        var sb = new System.Text.StringBuilder(4096);
        sb.AppendLine("=== Candidate CV summary ===");
        sb.AppendLine(cvSummaryJson);
        sb.AppendLine();
        sb.AppendLine("=== Vacancy analysis ===");
        sb.AppendLine(vacancyAnalysisJson);
        if (evidence.ValueKind == JsonValueKind.Object)
        {
            sb.AppendLine();
            sb.AppendLine("=== Match evidence ===");
            sb.AppendLine(evidence.GetRawText());
        }
        return sb.ToString();
    }

    private static List<string> ReadStringArray(JsonElement obj, string field)
    {
        var list = new List<string>();
        if (obj.ValueKind != JsonValueKind.Object) return list;
        if (!obj.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array) return list;
        foreach (var it in arr.EnumerateArray())
            if (it.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(it.GetString()))
                list.Add(it.GetString()!);
        return list;
    }

    private static bool CalibrationOk(string reasonEn, double score)
    {
        if (string.IsNullOrWhiteSpace(reasonEn)) return false;
        var expected = score >= 0.75 ? "Strong"
            : score >= 0.50 ? "Partial"
            : score >= 0.25 ? "Weak"
            : "Mismatch";


        var head = reasonEn.Length > 120 ? reasonEn[..120] : reasonEn;
        return head.Contains(expected, StringComparison.OrdinalIgnoreCase);
    }

    private static bool NoStrictHallucination(
        string reasonEn,
        IReadOnlyList<string> missing,
        IReadOnlyList<string> antiFlags)
    {
        if (string.IsNullOrWhiteSpace(reasonEn)) return true;
        var idx = reasonEn.IndexOf("Gaps:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return true;
        var tail = reasonEn[(idx + "Gaps:".Length)..];
        var endIdx = tail.IndexOfAny(new[] { '.', '\n' });
        if (endIdx >= 0) tail = tail[..endIdx];

        var missingLower = missing.Select(m => m.ToLowerInvariant()).ToHashSet();
        var antiLower = antiFlags.Select(a => a.ToLowerInvariant()).ToHashSet();

        foreach (var raw in tail.Split(new[] { ',', ';' }))
        {
            var token = raw.Trim(' ', '.', ',', ';').ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(token) || token == "none") continue;
            token = Regex.Replace(token, @"^(missing|no)\s+", "");

            bool matched = missingLower.Any(m => m.Contains(token) || token.Contains(m))
                        || antiLower.Any(a => a.Contains(token) || token.Contains(a));
            if (!matched) return false;
        }
        return true;
    }

    private static bool BilingualBalanced(string reasonEn, string reasonUk)
    {
        if (string.IsNullOrWhiteSpace(reasonEn) || string.IsNullOrWhiteSpace(reasonUk))
            return false;
        var en = reasonEn.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        var uk = reasonUk.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return Math.Abs(en - uk) <= 4;
    }

    private static bool LengthOk(string reasonEn)
    {
        if (string.IsNullOrWhiteSpace(reasonEn)) return false;
        var n = reasonEn.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
        return n is >= 5 and <= 30;
    }

    private static bool FormatOk(string reasonEn) =>
        !string.IsNullOrWhiteSpace(reasonEn)
        && (reasonEn.Contains("Strengths:", StringComparison.OrdinalIgnoreCase)
            || reasonEn.Contains("Gaps:", StringComparison.OrdinalIgnoreCase));

    private static bool HasContextLead(string reasonEn)
    {
        if (string.IsNullOrWhiteSpace(reasonEn)) return false;
        var lower = reasonEn.ToLowerInvariant();
        return lower.Contains("overqualified")
            || lower.Contains("underqualified")
            || lower.Contains("cross-domain")
            || lower.Contains("cross domain")
            || lower.Contains("role family")
            || lower.Contains("different role")
            || lower.Contains("career switcher")
            || lower.Contains("transition")
            || lower.Contains("переоцінений")
            || lower.Contains("недокваліфікований");
    }

    private static bool AntiFlagSurfaced(string reasonEn, IReadOnlyList<string> antiFlags)
    {

        if (antiFlags.Count == 0) return true;
        if (string.IsNullOrWhiteSpace(reasonEn)) return false;


        var lower = reasonEn.ToLowerInvariant();
        return antiFlags.Any(af =>
        {
            var aLower = af.ToLowerInvariant();


            var tokens = aLower.Split(new[] { ' ', '-', '_', '/' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(t => t.Length > 3).ToList();
            return tokens.Count == 0
                ? lower.Contains(aLower)
                : tokens.Any(t => lower.Contains(t));
        });
    }
}
