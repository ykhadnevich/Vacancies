using System.Globalization;
using System.Text;
using System.Text.Json;
using EvalTool.Baselines;
using EvalTool.Calibration;
using EvalTool.Metrics;
using EvalTool.Pipeline;
using Microsoft.Extensions.Logging;

namespace EvalTool.Evaluation;

/// <summary>
/// Step-6 one-shot orchestrator: runs the full held-out evaluation pipeline
/// for a single prompt version end-to-end, then (optionally) diffs the
/// result against a previously-evaluated version and surfaces per-pair
/// regressions.
///
/// Replaces the manual six-command workflow with a single
/// <c>evaluate-version --version v1_7 --compare-to v1_6</c> invocation
/// that produces:
/// <list type="bullet">
///   <item><c>results/heldout_&lt;version&gt;.json</c> — production predictions</item>
///   <item><c>results/metrics_&lt;version&gt;/</c> — Spearman/QWK/NDCG/ECE</item>
///   <item><c>results/ablation_caps_&lt;version&gt;/</c> — caps trade-off</item>
///   <item><c>results/calibration_&lt;version&gt;/</c> — isotonic + Platt calibration</item>
///   <item><c>results/comparison_&lt;version&gt;_vs_&lt;compare_to&gt;.md</c> —
///         metric deltas + regression pair table</item>
/// </list>
///
/// Designed to be the regression-test command run on every prompt change
/// after the thesis defence — see <c>EvalTool/HELDOUT_RUNBOOK.md</c>.
/// </summary>
public sealed class VersionEvaluator
{
    private readonly HeldoutScorer _scorer;
    private readonly BaselineRunner _baselines;
    private readonly HeldoutMetricsRunner _metrics;
    private readonly CapsAblationRunner _caps;
    private readonly CalibrationFitter _calibration;
    private readonly ILogger<VersionEvaluator> _logger;

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public VersionEvaluator(
        HeldoutScorer scorer,
        BaselineRunner baselines,
        HeldoutMetricsRunner metrics,
        CapsAblationRunner caps,
        CalibrationFitter calibration,
        ILogger<VersionEvaluator> logger)
    {
        _scorer = scorer;
        _baselines = baselines;
        _metrics = metrics;
        _caps = caps;
        _calibration = calibration;
        _logger = logger;
    }

    public async Task RunAsync(
        string version,
        string? compareTo,
        string goldPath,
        string cvDir,
        string vacancyDir,
        string baselinePath,
        string resultsRoot,
        double regressionThresholdAbsErr = 0.10,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(resultsRoot);

        // ── 1. Score held-out (idempotent — skip if recent file with same n_pairs exists) ──
        var predsPath = Path.Combine(resultsRoot, $"heldout_{version}.json");
        if (File.Exists(predsPath))
        {
            try
            {
                using var existing = JsonDocument.Parse(
                    await File.ReadAllTextAsync(predsPath, ct));
                if (existing.RootElement.TryGetProperty("n_pairs", out var npEl)
                    && npEl.GetInt32() > 0)
                {
                    _logger.LogInformation(
                        "[1/5] score-heldout skipped — {Path} already present with n={N}. " +
                        "Delete the file to force re-scoring.",
                        predsPath, npEl.GetInt32());
                    goto skipScoring;
                }
            }
            catch { /* fall through to re-score on parse failure */ }
        }
        _logger.LogInformation("[1/5] score-heldout → {Path}", predsPath);
        await _scorer.RunAsync(goldPath, cvDir, vacancyDir, predsPath, concurrency: 4, ct: ct);
        skipScoring:;

        // ── 2. Baselines (idempotent — skip if present and gold unchanged) ──
        if (!File.Exists(baselinePath))
        {
            _logger.LogInformation("[2/5] baselines → {Path}", baselinePath);
            await _baselines.RunAsync(goldPath, cvDir, vacancyDir, baselinePath, ct);
        }
        else
        {
            _logger.LogInformation("[2/5] baselines skipped — {Path} already present", baselinePath);
        }

        // ── 3. Metrics ──────────────────────────────────────────────────
        var metricsDir = Path.Combine(resultsRoot, $"metrics_{version}");
        _logger.LogInformation("[3/5] compute-metrics → {Dir}", metricsDir);
        await _metrics.RunAsync(predsPath, baselinePath, metricsDir, ct);

        // ── 4. Caps ablation ────────────────────────────────────────────
        var capsDir = Path.Combine(resultsRoot, $"ablation_caps_{version}");
        _logger.LogInformation("[4/5] ablation-caps → {Dir}", capsDir);
        await _caps.RunAsync(predsPath, cvDir, vacancyDir, capsDir, ct);

        // ── 5. Calibration ──────────────────────────────────────────────
        var calibDir = Path.Combine(resultsRoot, $"calibration_{version}");
        _logger.LogInformation("[5/5] fit-calibration → {Dir}", calibDir);
        await _calibration.RunAsync(predsPath, calibDir, ct);

        // ── 6. Comparison report (optional) ─────────────────────────────
        if (!string.IsNullOrEmpty(compareTo))
        {
            var prevPredsPath = Path.Combine(resultsRoot, $"heldout_{compareTo}.json");
            var prevMetricsDir = Path.Combine(resultsRoot, $"metrics_{compareTo}");
            if (!File.Exists(prevPredsPath))
            {
                _logger.LogWarning(
                    "Skipping comparison — previous predictions file not found: {Path}",
                    prevPredsPath);
                return;
            }
            if (!Directory.Exists(prevMetricsDir))
            {
                _logger.LogWarning(
                    "Skipping comparison — previous metrics dir not found: {Dir}",
                    prevMetricsDir);
                return;
            }
            var comparisonPath = Path.Combine(
                resultsRoot, $"comparison_{version}_vs_{compareTo}.md");
            _logger.LogInformation("[Δ] Comparison report → {Path}", comparisonPath);
            await WriteComparisonAsync(
                newVersion: version,
                oldVersion: compareTo,
                newPredsPath: predsPath,
                oldPredsPath: prevPredsPath,
                newMetricsPath: Path.Combine(metricsDir, "report.json"),
                oldMetricsPath: Path.Combine(prevMetricsDir, "report.json"),
                outPath: comparisonPath,
                regressionThreshold: regressionThresholdAbsErr,
                ct);
        }

        _logger.LogInformation("✓ evaluate-version done — {Resultsdir}", resultsRoot);
    }

    private async Task WriteComparisonAsync(
        string newVersion, string oldVersion,
        string newPredsPath, string oldPredsPath,
        string newMetricsPath, string oldMetricsPath,
        string outPath,
        double regressionThreshold,
        CancellationToken ct)
    {
        var newM = JsonSerializer.Deserialize<JsonElement>(
            await File.ReadAllTextAsync(newMetricsPath, ct));
        var oldM = JsonSerializer.Deserialize<JsonElement>(
            await File.ReadAllTextAsync(oldMetricsPath, ct));

        var newPreds = JsonSerializer.Deserialize<PredictionsFile>(
                          await File.ReadAllTextAsync(newPredsPath, ct), ReadOpts)
                       ?? throw new InvalidOperationException("Empty new predictions");
        var oldPreds = JsonSerializer.Deserialize<PredictionsFile>(
                          await File.ReadAllTextAsync(oldPredsPath, ct), ReadOpts)
                       ?? throw new InvalidOperationException("Empty old predictions");

        var inv = CultureInfo.InvariantCulture;
        var sb = new StringBuilder();
        sb.AppendLine($"# Version comparison — `{newVersion}` vs `{oldVersion}`\n");

        // ── Metric deltas ─────────────────────────────────────────────
        sb.AppendLine("## Overall metric deltas\n");
        sb.AppendLine($"| Metric | `{oldVersion}` | `{newVersion}` | Δ | Verdict |");
        sb.AppendLine("|---|---|---|---|---|");
        AppendDelta(sb, "Spearman ρ", oldM, newM, "overall", "spearman", higherIsBetter: true, inv);
        AppendDelta(sb, "Kendall τ", oldM, newM, "overall", "kendall_tau", higherIsBetter: true, inv);
        AppendDelta(sb, "Quadratic Weighted Kappa", oldM, newM, "overall", "quadratic_weighted_kappa", higherIsBetter: true, inv);
        AppendDelta(sb, "MAE (0-10)", oldM, newM, "overall", "mae_native_scale", higherIsBetter: false, inv);
        AppendDelta(sb, "NDCG@3", oldM, newM, "overall", "ndcg_at3", higherIsBetter: true, inv);
        AppendDelta(sb, "NDCG@5", oldM, newM, "overall", "ndcg_at5", higherIsBetter: true, inv);
        AppendDelta(sb, "Expected Calibration Error", oldM, newM, "overall", "expected_calibration_error", higherIsBetter: false, inv);
        sb.AppendLine();

        sb.AppendLine("## Per-subset Spearman ρ\n");
        sb.AppendLine($"| Subset | `{oldVersion}` | `{newVersion}` | Δ |");
        sb.AppendLine("|---|---|---|---|");
        foreach (var key in new[] { "safety_set", "coverage_strong_fit_set", "midrange_only" })
            AppendSubsetDelta(sb, key, oldM, newM, "spearman", inv);
        sb.AppendLine();

        // ── Per-pair regression detection ─────────────────────────────
        var oldByPair = oldPreds.Predictions
            .ToDictionary(p => (p.CvId, p.VacancyId), p => p);
        int unchanged = 0, improved = 0, regressed = 0;
        var regressions = new List<(string CvId, string VacancyId, double OldPred, double NewPred, double GoldNorm, double OldErr, double NewErr)>();
        var improvements = new List<(string CvId, string VacancyId, double OldPred, double NewPred, double GoldNorm, double OldErr, double NewErr)>();
        foreach (var np in newPreds.Predictions)
        {
            if (!oldByPair.TryGetValue((np.CvId, np.VacancyId), out var op)) continue;
            var oldErr = Math.Abs(op.PredictedScore - np.GoldNorm);
            var newErr = Math.Abs(np.PredictedScore - np.GoldNorm);
            if (newErr > oldErr + regressionThreshold)
            {
                regressed++;
                regressions.Add((np.CvId, np.VacancyId, op.PredictedScore, np.PredictedScore, np.GoldNorm, oldErr, newErr));
            }
            else if (oldErr > newErr + regressionThreshold)
            {
                improved++;
                improvements.Add((np.CvId, np.VacancyId, op.PredictedScore, np.PredictedScore, np.GoldNorm, oldErr, newErr));
            }
            else
            {
                unchanged++;
            }
        }
        sb.AppendLine($"## Per-pair movement (regression threshold: |err_new − err_old| > {regressionThreshold:F2})\n");
        sb.AppendLine($"- Improved pairs: **{improvements.Count}**");
        sb.AppendLine($"- Regressed pairs: **{regressions.Count}**");
        sb.AppendLine($"- Unchanged (within noise): {unchanged}\n");

        if (regressions.Count > 0)
        {
            sb.AppendLine($"### Regressed pairs (top {Math.Min(20, regressions.Count)} by Δerror)\n");
            sb.AppendLine($"| CV | Vacancy | Gold | `{oldVersion}` predicted | `{newVersion}` predicted | Δerror |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in regressions.OrderByDescending(r => r.NewErr - r.OldErr).Take(20))
            {
                sb.AppendLine($"| {r.CvId} | {r.VacancyId} | {r.GoldNorm.ToString("F3", inv)} | {r.OldPred.ToString("F3", inv)} | {r.NewPred.ToString("F3", inv)} | +{(r.NewErr - r.OldErr).ToString("F3", inv)} |");
            }
            sb.AppendLine();
        }

        if (improvements.Count > 0)
        {
            sb.AppendLine($"### Improved pairs (top {Math.Min(20, improvements.Count)} by Δerror)\n");
            sb.AppendLine($"| CV | Vacancy | Gold | `{oldVersion}` predicted | `{newVersion}` predicted | −Δerror |");
            sb.AppendLine("|---|---|---|---|---|---|");
            foreach (var r in improvements.OrderByDescending(r => r.OldErr - r.NewErr).Take(20))
            {
                sb.AppendLine($"| {r.CvId} | {r.VacancyId} | {r.GoldNorm.ToString("F3", inv)} | {r.OldPred.ToString("F3", inv)} | {r.NewPred.ToString("F3", inv)} | -{(r.OldErr - r.NewErr).ToString("F3", inv)} |");
            }
            sb.AppendLine();
        }

        // ── Verdict ───────────────────────────────────────────────────
        sb.AppendLine("## Verdict\n");
        var spOld = oldM.GetProperty("overall").GetProperty("spearman").GetDouble();
        var spNew = newM.GetProperty("overall").GetProperty("spearman").GetDouble();
        var eceOld = oldM.GetProperty("overall").GetProperty("expected_calibration_error").GetDouble();
        var eceNew = newM.GetProperty("overall").GetProperty("expected_calibration_error").GetDouble();
        var spImproved = spNew > spOld + 0.01;
        var eceImproved = eceNew < eceOld - 0.005;
        var significantRegression = regressions.Count > improvements.Count * 1.5;

        if (spImproved && !significantRegression)
            sb.AppendLine($"✅ **`{newVersion}` improves Spearman by {(spNew - spOld).ToString("F3", inv)} without a significant regression — ship it.**");
        else if (significantRegression)
            sb.AppendLine($"⚠️ **`{newVersion}` has {regressions.Count} regressed pairs vs {improvements.Count} improved — investigate the top regressions before shipping.**");
        else if (eceImproved)
            sb.AppendLine($"✅ **`{newVersion}` improves calibration (ECE −{(eceOld - eceNew).ToString("F4", inv)}) without significant ranking degradation — ship if calibration is the priority.**");
        else
            sb.AppendLine($"➖ **`{newVersion}` is essentially indistinguishable from `{oldVersion}` on the held-out — no clear ship signal either way.**");

        await File.WriteAllTextAsync(outPath, sb.ToString(), ct);
    }

    private static void AppendDelta(
        StringBuilder sb, string label,
        JsonElement oldM, JsonElement newM,
        string subsetKey, string metricKey,
        bool higherIsBetter, CultureInfo inv)
    {
        var oldV = oldM.GetProperty(subsetKey).GetProperty(metricKey).GetDouble();
        var newV = newM.GetProperty(subsetKey).GetProperty(metricKey).GetDouble();
        var delta = newV - oldV;
        var goodChange = higherIsBetter ? delta > 0 : delta < 0;
        var arrow = Math.Abs(delta) < 0.005 ? "—" : (goodChange ? "↑" : "↓");
        var sign = delta >= 0 ? "+" : "";
        var fmt = label.Contains("ECE") ? "F4" : "F3";
        sb.AppendLine(
            $"| {label} | {oldV.ToString(fmt, inv)} | {newV.ToString(fmt, inv)} | {sign}{delta.ToString(fmt, inv)} | {arrow} |");
    }

    private static void AppendSubsetDelta(
        StringBuilder sb, string subsetKey,
        JsonElement oldM, JsonElement newM,
        string metricKey, CultureInfo inv)
    {
        if (!oldM.TryGetProperty(subsetKey, out var oldS) || !newM.TryGetProperty(subsetKey, out var newS))
            return;
        if (!oldS.TryGetProperty(metricKey, out var ov) || !newS.TryGetProperty(metricKey, out var nv))
            return;
        var oldV = ov.GetDouble();
        var newV = nv.GetDouble();
        var delta = newV - oldV;
        var sign = delta >= 0 ? "+" : "";
        sb.AppendLine($"| {subsetKey} | {oldV.ToString("F3", inv)} | {newV.ToString("F3", inv)} | {sign}{delta.ToString("F3", inv)} |");
    }

    // ── DTOs for predictions diff ─────────────────────────────────────

    private sealed record PredictionsFile(
        string SchemaVersion,
        string? ScoringVersion,
        int NPairs,
        List<PredictionRow> Predictions);

    private sealed record PredictionRow(
        string CvId,
        string VacancyId,
        double GoldNorm,
        double PredictedScore);
}
