using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EvalTool.Metrics;

/// <summary>
/// Step-3 orchestrator: reads the predicted-score JSON produced by
/// <see cref="EvalTool.Pipeline.HeldoutScorer"/> (and optionally the
/// non-LLM baselines), computes the full set of thesis metrics via
/// <see cref="MetricsCalculator"/>, and emits two artefacts:
///
/// <list type="bullet">
///   <item><c>report.json</c> — full metric payload, machine-readable.</item>
///   <item><c>report.md</c> — markdown tables ready to paste into the thesis.</item>
/// </list>
///
/// Computes overall + subset breakdowns (safety vs coverage_strong_fit vs
/// midrange-only) + side-by-side comparison with TF-IDF / BM25 baselines if
/// the baseline_predictions.json file is present.
/// </summary>
public sealed class HeldoutMetricsRunner
{
    private readonly ILogger<HeldoutMetricsRunner> _logger;

    private static readonly JsonSerializerOptions ReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
    private static readonly JsonSerializerOptions ReadSnakeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private static readonly JsonSerializerOptions WriteOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly HashSet<string> SafetyCvs = new()
    {
        "3_junior_designer_career_switcher", "6_devops_junior", "6_hr_recruiter_generic",
        "8_healthcare_junior", "9_healthcare_senior", "10_legal_mid_corporate_lawyer",
        "11_education_senior_teacher", "12_finance_senior_accountant",
        "16_marketing_mid_growth", "18_academic_professor_humanities",
        "23_security_engineer_mid"
    };

    public HeldoutMetricsRunner(ILogger<HeldoutMetricsRunner> logger) => _logger = logger;

    public async Task RunAsync(
        string predictionsPath,
        string? baselinesPath,
        string outDir,
        CancellationToken ct = default)
    {
        var rows = await LoadPredictionsAsync(predictionsPath, ct);
        var version = await ExtractVersionAsync(predictionsPath, ct);
        _logger.LogInformation("Loaded {N} predictions; version={V}", rows.Count, version);

        Directory.CreateDirectory(outDir);

        var safety = rows.Where(r => SafetyCvs.Contains(r.CvId)).ToList();
        var nonSafety = rows.Where(r => !SafetyCvs.Contains(r.CvId)).ToList();
        var midRange = rows.Where(r => r.Gold is 4 or 6 or 8 or 10).ToList();

        var report = new Dictionary<string, object?>
        {
            ["version"]                 = version,
            ["n_pairs"]                 = rows.Count,
            ["overall"]                 = ComputeMetrics(rows, "overall"),
            ["safety_set"]              = ComputeMetrics(safety, "safety_set (fresh CV × tech vacancy)"),
            ["coverage_strong_fit_set"] = ComputeMetrics(nonSafety, "coverage + strong_fit (dev CV × never-paired)"),
            ["midrange_only"]           = ComputeMetrics(midRange, "midrange (gold ∈ {4,6,8,10})")
        };

        Dictionary<string, object?>? baselinesCmp = null;
        if (!string.IsNullOrWhiteSpace(baselinesPath) && File.Exists(baselinesPath))
        {
            baselinesCmp = await ComputeBaselineComparisonAsync(baselinesPath, rows, ct);
            if (baselinesCmp is not null)
                report["baselines_overall"] = baselinesCmp;
        }

        report["cost"] = new Dictionary<string, object?>
        {
            ["total_input_tokens"]  = rows.Sum(r => (long)r.InputTokens),
            ["total_output_tokens"] = rows.Sum(r => (long)r.OutputTokens),
            ["total_usd"]           = Math.Round(rows.Sum(r => r.EstimatedCostUsd), 4),
            ["mean_latency_ms"]     = Math.Round(rows.Average(r => (double)r.LatencyMs), 1),
            ["p95_latency_ms"]      = Math.Round(Percentile(rows.Select(r => (double)r.LatencyMs).ToArray(), 95), 1)
        };

        var jsonPath = Path.Combine(outDir, "report.json");
        var mdPath   = Path.Combine(outDir, "report.md");

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(report, WriteOpts), ct);
        await File.WriteAllTextAsync(mdPath, RenderMarkdown(report, version), ct);

        _logger.LogInformation("Saved {Json} + {Md}", jsonPath, mdPath);

        // Console summary
        var overall = (MetricsResult)report["overall"]!;
        _logger.LogInformation("=== Overall ===  ρ={Sp:F3}  τ={Kt:F3}  QWK={Q:F3}  MAE={M:F2}  NDCG@3={N3:F3}  NDCG@5={N5:F3}  ECE={E:F3}",
            overall.Spearman, overall.KendallTau, overall.QuadraticWeightedKappa,
            overall.MaeNativeScale, overall.NdcgAt3, overall.NdcgAt5, overall.ExpectedCalibrationError);
    }

    // ── Loading ─────────────────────────────────────────────────────────

    private async Task<List<PredictionRow>> LoadPredictionsAsync(string path, CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(path, ct);
        var doc = JsonSerializer.Deserialize<PredictionsFile>(text, ReadSnakeOpts)
                  ?? throw new InvalidOperationException("Empty predictions file");
        return doc.Predictions;
    }

    private async Task<string> ExtractVersionAsync(string path, CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(await File.ReadAllTextAsync(path, ct));
        return doc.RootElement.TryGetProperty("scoring_version", out var v)
            ? v.GetString() ?? "unknown" : "unknown";
    }

    private async Task<Dictionary<string, object?>?> ComputeBaselineComparisonAsync(
        string path, List<PredictionRow> rows, CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(path, ct);
        using var doc = JsonDocument.Parse(text);
        if (!doc.RootElement.TryGetProperty("predictions", out var predsEl)) return null;

        var lookup = new Dictionary<(string, string), (double tfidf, double bm25)>();
        foreach (var p in predsEl.EnumerateArray())
        {
            var cv = p.GetProperty("cv_id").GetString()!;
            var vac = p.GetProperty("vacancy_id").GetString()!;
            var tf = p.GetProperty("tfidf_cosine").GetDouble();
            var bm = p.GetProperty("bm25_norm").GetDouble();
            lookup[(cv, vac)] = (tf, bm);
        }

        var matched = rows.Select(r => (r, lookup.TryGetValue((r.CvId, r.VacancyId), out var b) ? b : default))
                         .Where(t => t.Item2 != default)
                         .ToList();
        if (matched.Count == 0) return null;

        var tfidf = matched.Select(t => t.Item2.tfidf).ToArray();
        var bm25  = matched.Select(t => t.Item2.bm25).ToArray();
        var gold  = matched.Select(t => t.r.GoldNorm).ToArray();
        var goldNative = matched.Select(t => (double)t.r.Gold).ToArray();

        var spTf = MetricsCalculator.Spearman(tfidf, gold);
        var spBm = MetricsCalculator.Spearman(bm25, gold);
        var maeTf = MetricsCalculator.MeanAbsoluteError(tfidf.Select(v => v * 10).ToArray(), goldNative);
        var maeBm = MetricsCalculator.MeanAbsoluteError(bm25.Select(v => v * 10).ToArray(), goldNative);

        return new Dictionary<string, object?>
        {
            ["n_pairs"] = matched.Count,
            ["tfidf_cosine"] = new Dictionary<string, object?>
            {
                ["spearman"]         = Math.Round(spTf, 4),
                ["mae_native_scale"] = Math.Round(maeTf, 3)
            },
            ["bm25_norm"] = new Dictionary<string, object?>
            {
                ["spearman"]         = Math.Round(spBm, 4),
                ["mae_native_scale"] = Math.Round(maeBm, 3)
            }
        };
    }

    // ── Per-subset computation ──────────────────────────────────────────

    private MetricsResult ComputeMetrics(List<PredictionRow> rows, string label)
    {
        if (rows.Count == 0)
            return new MetricsResult(label, 0, 0, new[] { 0.0, 0.0 }, 0, 0,
                0, new[] { 0.0, 0.0 }, 0, 0, 0, new List<MetricsCalculator.CalibrationBin>());

        var pred = rows.Select(r => r.PredictedScore).ToArray();
        var goldNorm = rows.Select(r => r.GoldNorm).ToArray();
        var goldNative = rows.Select(r => (double)r.Gold).ToArray();
        var goldNativeInt = rows.Select(r => r.Gold).ToArray();
        var predQuant = rows.Select(r => MetricsCalculator.QuantiseToAnchor(r.PredictedScore)).ToArray();

        var spearman = MetricsCalculator.Spearman(pred, goldNorm);
        var kendall  = MetricsCalculator.KendallTau(pred, goldNorm);
        var qwk      = MetricsCalculator.QuadraticWeightedKappa(goldNativeInt, predQuant);
        var mae      = MetricsCalculator.MeanAbsoluteError(
                          pred.Select(p => p * 10).ToArray(), goldNative);

        var groupPairs = rows.Select(r => (r.CvId, r.PredictedScore, r.GoldNorm));
        var ndcg3 = MetricsCalculator.NdcgAtK(groupPairs, 3);
        var ndcg5 = MetricsCalculator.NdcgAtK(groupPairs, 5);

        var (bins, ece) = MetricsCalculator.CalibrationBins(pred, goldNorm);

        var (spLo, spHi) = MetricsCalculator.BootstrapCi(MetricsCalculator.Spearman, pred, goldNorm);
        var (maeLo, maeHi) = MetricsCalculator.BootstrapCi(
            (a, b) => MetricsCalculator.MeanAbsoluteError(
                          a.Select(v => v * 10).ToArray(), b), pred, goldNative);

        return new MetricsResult(
            Label: label,
            N: rows.Count,
            Spearman: Math.Round(spearman, 4),
            SpearmanCi95: new[] { Math.Round(spLo, 4), Math.Round(spHi, 4) },
            KendallTau: Math.Round(kendall, 4),
            QuadraticWeightedKappa: Math.Round(qwk, 4),
            MaeNativeScale: Math.Round(mae, 3),
            MaeNativeCi95: new[] { Math.Round(maeLo, 3), Math.Round(maeHi, 3) },
            NdcgAt3: Math.Round(ndcg3, 4),
            NdcgAt5: Math.Round(ndcg5, 4),
            ExpectedCalibrationError: Math.Round(ece, 4),
            CalibrationBins: bins);
    }

    // ── Markdown rendering ──────────────────────────────────────────────

    private static string RenderMarkdown(Dictionary<string, object?> report, string version)
    {
        var sb = new StringBuilder();
        var overall = (MetricsResult)report["overall"]!;
        var cost = (Dictionary<string, object?>)report["cost"]!;

        sb.AppendLine($"# Held-out evaluation report — `{version}`\n");
        sb.AppendLine($"N pairs: **{report["n_pairs"]}**  ·  Cost: **${cost["total_usd"]:F3}** ·  ");
        sb.AppendLine($"Mean latency: **{cost["mean_latency_ms"]} ms** · p95: **{cost["p95_latency_ms"]} ms**\n");

        sb.AppendLine("## Overall metrics\n");
        sb.AppendLine("| Metric | Value | 95% CI |");
        sb.AppendLine("|---|---|---|");
        sb.AppendLine($"| Spearman ρ | {overall.Spearman.ToString("F3", CultureInfo.InvariantCulture)} | [{overall.SpearmanCi95[0]}, {overall.SpearmanCi95[1]}] |");
        sb.AppendLine($"| Kendall τ | {overall.KendallTau.ToString("F3", CultureInfo.InvariantCulture)} | — |");
        sb.AppendLine($"| Quadratic Weighted Kappa | {overall.QuadraticWeightedKappa.ToString("F3", CultureInfo.InvariantCulture)} | — |");
        sb.AppendLine($"| MAE (0-10 scale) | {overall.MaeNativeScale.ToString("F3", CultureInfo.InvariantCulture)} | [{overall.MaeNativeCi95[0]}, {overall.MaeNativeCi95[1]}] |");
        sb.AppendLine($"| NDCG@3 (per-CV avg) | {overall.NdcgAt3.ToString("F3", CultureInfo.InvariantCulture)} | — |");
        sb.AppendLine($"| NDCG@5 (per-CV avg) | {overall.NdcgAt5.ToString("F3", CultureInfo.InvariantCulture)} | — |");
        sb.AppendLine($"| Expected Calibration Error | {overall.ExpectedCalibrationError.ToString("F4", CultureInfo.InvariantCulture)} | — |");
        sb.AppendLine();

        if (report.TryGetValue("baselines_overall", out var blObj) && blObj is Dictionary<string, object?> bl)
        {
            var tfidf = (Dictionary<string, object?>)bl["tfidf_cosine"]!;
            var bm25  = (Dictionary<string, object?>)bl["bm25_norm"]!;
            sb.AppendLine("## Comparison vs non-LLM baselines\n");
            sb.AppendLine("| Method | Spearman ρ | MAE (0-10) |");
            sb.AppendLine("|---|---|---|");
            sb.AppendLine($"| **Gemini Mono (`{version}`)** | **{overall.Spearman.ToString("F3", CultureInfo.InvariantCulture)}** | **{overall.MaeNativeScale.ToString("F3", CultureInfo.InvariantCulture)}** |");
            sb.AppendLine($"| TF-IDF cosine (char_wb 3-5) | {tfidf["spearman"]} | {tfidf["mae_native_scale"]} |");
            sb.AppendLine($"| BM25 (per-CV norm) | {bm25["spearman"]} | {bm25["mae_native_scale"]} |");
            sb.AppendLine();
        }

        sb.AppendLine("## Subset breakdown\n");
        sb.AppendLine("| Subset | n | Spearman | QWK | MAE | ECE |");
        sb.AppendLine("|---|---|---|---|---|---|");
        foreach (var (key, label) in new[]
        {
            ("safety_set", "safety_set"),
            ("coverage_strong_fit_set", "coverage_strong_fit"),
            ("midrange_only", "midrange")
        })
        {
            if (report.TryGetValue(key, out var sObj) && sObj is MetricsResult s)
                sb.AppendLine($"| {label} | {s.N} | " +
                              $"{s.Spearman.ToString("F3", CultureInfo.InvariantCulture)} | " +
                              $"{s.QuadraticWeightedKappa.ToString("F3", CultureInfo.InvariantCulture)} | " +
                              $"{s.MaeNativeScale.ToString("F3", CultureInfo.InvariantCulture)} | " +
                              $"{s.ExpectedCalibrationError.ToString("F4", CultureInfo.InvariantCulture)} |");
        }
        sb.AppendLine();

        sb.AppendLine("## Reliability diagram (calibration)\n");
        sb.AppendLine("| Bin | n | Mean predicted | Mean gold | \\|Δ\\| |");
        sb.AppendLine("|---|---|---|---|---|");
        foreach (var b in overall.CalibrationBins)
        {
            if (b.N == 0)
                sb.AppendLine($"| [{b.BinLo:F1}, {b.BinHi:F1}] | 0 | — | — | — |");
            else
                sb.AppendLine($"| [{b.BinLo:F1}, {b.BinHi:F1}] | {b.N} | " +
                              $"{b.MeanPredicted:F3} | {b.MeanGold:F3} | {b.AbsGap:F3} |");
        }

        return sb.ToString();
    }

    private static double Percentile(double[] xs, double p)
    {
        if (xs.Length == 0) return 0;
        var sorted = xs.OrderBy(v => v).ToArray();
        var rank = (p / 100.0) * (sorted.Length - 1);
        var lower = (int)Math.Floor(rank);
        var upper = (int)Math.Ceiling(rank);
        if (lower == upper) return sorted[lower];
        return sorted[lower] + (rank - lower) * (sorted[upper] - sorted[lower]);
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
        int InputTokens,
        int OutputTokens,
        double EstimatedCostUsd,
        long LatencyMs);

    public sealed record MetricsResult(
        string Label,
        int N,
        double Spearman,
        double[] SpearmanCi95,
        double KendallTau,
        double QuadraticWeightedKappa,
        double MaeNativeScale,
        double[] MaeNativeCi95,
        double NdcgAt3,
        double NdcgAt5,
        double ExpectedCalibrationError,
        List<MetricsCalculator.CalibrationBin> CalibrationBins);
}
