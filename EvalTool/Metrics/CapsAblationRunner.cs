using System.Globalization;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Microsoft.Extensions.Logging;

namespace EvalTool.Metrics;

/// <summary>
/// Caps on/off ablation orchestrator for the held-out evaluation.
///
/// The <see cref="EvalTool.Pipeline.HeldoutScorer"/> output records the raw
/// linear composite (weighted sum of sub-scores × anti-flag penalty) — the
/// score the LLM "wanted to give". The <see cref="IScoringCapService"/> is
/// applied externally (in production by <c>AnalyzeListAgainstVacancyHandler</c>)
/// and pulls outliers down based on structural rules
/// (seniority gap, language gap, experience gap, combined experience+seniority).
///
/// This runner applies those same caps **offline** to the existing predictions
/// file, then computes the full metric set for both variants side-by-side.
/// The result answers the ablation question:
/// "Does <c>ScoringCapService</c> add measurable value over the bare LLM composite?"
///
/// No re-run of Gemini is required — the ablation is deterministic from the
/// already-recorded sub-scores plus the CV/vacancy JSON files needed for the
/// language-gap detector.
/// </summary>
public sealed class CapsAblationRunner
{
    private readonly IScoringCapService _caps;
    private readonly ILogger<CapsAblationRunner> _logger;

    private static readonly JsonSerializerOptions ReadSnakeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public CapsAblationRunner(IScoringCapService caps, ILogger<CapsAblationRunner> logger)
    {
        _caps = caps;
        _logger = logger;
    }

    public async Task RunAsync(
        string predictionsPath,
        string cvDir,
        string vacancyDir,
        string outDir,
        CancellationToken ct = default)
    {
        var text = await File.ReadAllTextAsync(predictionsPath, ct);
        var doc = JsonSerializer.Deserialize<PredictionsFile>(text, ReadSnakeOpts)
                  ?? throw new InvalidOperationException("Empty predictions file");
        var rows = doc.Predictions;
        var version = doc.ScoringVersion ?? "unknown";
        _logger.LogInformation("Loaded {N} predictions; version={V}", rows.Count, version);

        Directory.CreateDirectory(outDir);

        // Apply caps offline to each row
        var capped = new List<EvalRow>(rows.Count);
        var uncapped = new List<EvalRow>(rows.Count);
        var capFiredCount = 0;
        foreach (var r in rows)
        {
            using var cvDoc = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(cvDir, $"{r.CvId}.json"), ct));
            using var vacDoc = JsonDocument.Parse(
                await File.ReadAllTextAsync(Path.Combine(vacancyDir, $"{r.VacancyId}.json"), ct));
            var languageGap = LanguageGapDetector.IsLanguageRequirementAbove(
                cvDoc.RootElement, vacDoc.RootElement);

            var subs = new SubScores(
                SkillMatch:      r.SubScores.SkillMatch,
                SeniorityMatch:  r.SubScores.SeniorityMatch,
                ExperienceMatch: r.SubScores.ExperienceMatch,
                LanguageMatch:   r.SubScores.LanguageMatch,
                EducationMatch:  r.SubScores.EducationMatch,
                RoleIntentMatch: r.SubScores.RoleIntentMatch,
                DomainAlignment: r.SubScores.DomainAlignment);

            var cappedScore = _caps.ApplyCaps(r.PredictedScore, subs, languageGap);
            if (Math.Abs(cappedScore - r.PredictedScore) > 1e-9) capFiredCount++;

            uncapped.Add(new EvalRow(r.CvId, r.Gold, r.GoldNorm, r.PredictedScore));
            capped.Add(new EvalRow(r.CvId, r.Gold, r.GoldNorm, cappedScore));
        }

        _logger.LogInformation(
            "Caps fired on {N}/{Total} pairs ({Pct:F1}%)",
            capFiredCount, rows.Count, 100.0 * capFiredCount / rows.Count);

        // Compute metrics for both variants
        var metricsOff = ComputeAll(uncapped);
        var metricsOn  = ComputeAll(capped);

        // Save side-by-side report
        var report = new Dictionary<string, object?>
        {
            ["version"]              = version,
            ["n_pairs"]              = rows.Count,
            ["n_caps_fired"]         = capFiredCount,
            ["pct_caps_fired"]       = Math.Round(100.0 * capFiredCount / rows.Count, 2),
            ["caps_off"]             = metricsOff,
            ["caps_on"]              = metricsOn
        };
        await File.WriteAllTextAsync(
            Path.Combine(outDir, "report.json"),
            JsonSerializer.Serialize(report, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }), ct);

        await File.WriteAllTextAsync(
            Path.Combine(outDir, "report.md"),
            RenderMd(version, rows.Count, capFiredCount, metricsOff, metricsOn), ct);

        _logger.LogInformation("Saved ablation report to {Dir}", outDir);
        _logger.LogInformation(
            "=== caps OFF vs caps ON ===  ρ {SpOff:F3} → {SpOn:F3}  QWK {QwkOff:F3} → {QwkOn:F3}  MAE {MaeOff:F2} → {MaeOn:F2}  ECE {EceOff:F4} → {EceOn:F4}",
            metricsOff.Spearman, metricsOn.Spearman,
            metricsOff.Qwk, metricsOn.Qwk,
            metricsOff.Mae, metricsOn.Mae,
            metricsOff.Ece, metricsOn.Ece);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static AblationMetrics ComputeAll(List<EvalRow> rows)
    {
        var pred = rows.Select(r => r.Predicted).ToArray();
        var goldNorm = rows.Select(r => r.GoldNorm).ToArray();
        var goldNative = rows.Select(r => (double)r.Gold).ToArray();
        var goldInt = rows.Select(r => r.Gold).ToArray();
        var predQuant = rows.Select(r => MetricsCalculator.QuantiseToAnchor(r.Predicted)).ToArray();

        var sp = MetricsCalculator.Spearman(pred, goldNorm);
        var kt = MetricsCalculator.KendallTau(pred, goldNorm);
        var qwk = MetricsCalculator.QuadraticWeightedKappa(goldInt, predQuant);
        var mae = MetricsCalculator.MeanAbsoluteError(
                      pred.Select(p => p * 10).ToArray(), goldNative);
        var groupPairs = rows.Select(r => (r.CvId, r.Predicted, r.GoldNorm));
        var ndcg3 = MetricsCalculator.NdcgAtK(groupPairs, 3);
        var ndcg5 = MetricsCalculator.NdcgAtK(groupPairs, 5);
        var (_, ece) = MetricsCalculator.CalibrationBins(pred, goldNorm);
        var (spLo, spHi) = MetricsCalculator.BootstrapCi(MetricsCalculator.Spearman, pred, goldNorm);

        return new AblationMetrics(
            N: rows.Count,
            Spearman: Math.Round(sp, 4),
            SpearmanCi: new[] { Math.Round(spLo, 4), Math.Round(spHi, 4) },
            Kendall: Math.Round(kt, 4),
            Qwk: Math.Round(qwk, 4),
            Mae: Math.Round(mae, 3),
            Ndcg3: Math.Round(ndcg3, 4),
            Ndcg5: Math.Round(ndcg5, 4),
            Ece: Math.Round(ece, 4));
    }

    private static string RenderMd(
        string version, int n, int capsFired,
        AblationMetrics off, AblationMetrics on)
    {
        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"# Caps on/off ablation — `{version}`\n");
        sb.AppendLine($"N pairs: **{n}**  ·  Caps fired on **{capsFired}** pairs ({100.0 * capsFired / n:F1}% of dataset)\n");
        sb.AppendLine("## Side-by-side metrics\n");
        sb.AppendLine("| Metric | Caps OFF (LLM raw composite) | Caps ON (production) | Δ |");
        sb.AppendLine("|---|---|---|---|");
        sb.AppendLine($"| Spearman ρ | {off.Spearman.ToString("F3", inv)} | **{on.Spearman.ToString("F3", inv)}** | {Diff(off.Spearman, on.Spearman, inv)} |");
        sb.AppendLine($"| Spearman 95% CI | [{off.SpearmanCi[0]}, {off.SpearmanCi[1]}] | [{on.SpearmanCi[0]}, {on.SpearmanCi[1]}] | — |");
        sb.AppendLine($"| Kendall τ | {off.Kendall.ToString("F3", inv)} | {on.Kendall.ToString("F3", inv)} | {Diff(off.Kendall, on.Kendall, inv)} |");
        sb.AppendLine($"| Quadratic Weighted Kappa | {off.Qwk.ToString("F3", inv)} | **{on.Qwk.ToString("F3", inv)}** | {Diff(off.Qwk, on.Qwk, inv)} |");
        sb.AppendLine($"| MAE (0-10 scale) | {off.Mae.ToString("F3", inv)} | **{on.Mae.ToString("F3", inv)}** | {Diff(on.Mae, off.Mae, inv, lowerIsBetter: true)} |");
        sb.AppendLine($"| NDCG@3 | {off.Ndcg3.ToString("F3", inv)} | {on.Ndcg3.ToString("F3", inv)} | {Diff(off.Ndcg3, on.Ndcg3, inv)} |");
        sb.AppendLine($"| NDCG@5 | {off.Ndcg5.ToString("F3", inv)} | {on.Ndcg5.ToString("F3", inv)} | {Diff(off.Ndcg5, on.Ndcg5, inv)} |");
        sb.AppendLine($"| Expected Calibration Error | {off.Ece.ToString("F4", inv)} | **{on.Ece.ToString("F4", inv)}** | {Diff(on.Ece, off.Ece, inv, lowerIsBetter: true)} |");
        sb.AppendLine();
        sb.AppendLine("## Interpretation\n");
        sb.AppendLine("- **Caps OFF** = raw LLM composite (weighted sum of 7 sub-scores × anti-flag penalty). Reflects pure prompt quality without structural safety nets.");
        sb.AppendLine("- **Caps ON** = production behaviour. Adds rule-based caps for seniority gap, experience gap, language gap, combined experience+seniority, and a domain-alignment subtractor.");
        sb.AppendLine("- Higher Spearman / QWK / NDCG and lower MAE / ECE are better. Bold = the better cell per metric.");
        sb.AppendLine();
        return sb.ToString();
    }

    private static string Diff(double a, double b, CultureInfo inv, bool lowerIsBetter = false)
    {
        var delta = b - a;
        var sign = delta >= 0 ? "+" : "";
        var marker = (lowerIsBetter ? delta < 0 : delta > 0) ? " ↑" : (delta == 0 ? "" : " ↓");
        return $"{sign}{delta.ToString("F3", inv)}{marker}";
    }

    // ── DTOs ────────────────────────────────────────────────────────────

    private sealed record PredictionsFile(
        string SchemaVersion,
        string? ScoringVersion,
        int NPairs,
        List<PredictionRow> Predictions);

    private sealed record PredictionRow(
        string CvId,
        string VacancyId,
        int Gold,
        double GoldNorm,
        double PredictedScore,
        SubScoreDto SubScores);

    private sealed record SubScoreDto(
        double SkillMatch,
        double SeniorityMatch,
        double ExperienceMatch,
        double LanguageMatch,
        double EducationMatch,
        double RoleIntentMatch,
        double DomainAlignment);

    private sealed record EvalRow(string CvId, int Gold, double GoldNorm, double Predicted);

    public sealed record AblationMetrics(
        int N,
        double Spearman,
        double[] SpearmanCi,
        double Kendall,
        double Qwk,
        double Mae,
        double Ndcg3,
        double Ndcg5,
        double Ece);
}
