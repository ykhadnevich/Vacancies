using System.Globalization;
using System.Text;
using System.Text.Json;
using EvalTool.Metrics;
using Microsoft.Extensions.Logging;

namespace EvalTool.Calibration;

/// <summary>
/// Step-5 calibration orchestrator: takes a held-out predictions file
/// (output of <c>score-heldout</c>) plus the held-out gold (Opus ratings),
/// fits both isotonic and Platt calibrators, evaluates them on the same
/// data via 5-fold cross-validation, and persists the best calibrator
/// as a portable JSON blob that the production scoring service can load
/// to produce calibrated percentages.
///
/// The orchestrator also writes a markdown report comparing the
/// before-calibration and after-calibration metrics (ECE, MAE,
/// reliability diagram) so the thesis can document the calibration
/// improvement as a concrete number rather than a "future work" item.
/// </summary>
public sealed class CalibrationFitter
{
    private readonly ILogger<CalibrationFitter> _logger;

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

    public CalibrationFitter(ILogger<CalibrationFitter> logger) => _logger = logger;

    public async Task RunAsync(
        string predictionsPath,
        string outDir,
        CancellationToken ct = default)
    {
        var doc = JsonSerializer.Deserialize<PredictionsFile>(
                      await File.ReadAllTextAsync(predictionsPath, ct), ReadSnakeOpts)
                  ?? throw new InvalidOperationException("Empty predictions file");
        var rows = doc.Predictions;
        var version = doc.ScoringVersion ?? "unknown";
        _logger.LogInformation("Loaded {N} predictions; version={V}", rows.Count, version);

        Directory.CreateDirectory(outDir);

        var xRaw = rows.Select(r => r.PredictedScore).ToArray();
        var yGoldNorm = rows.Select(r => r.GoldNorm).ToArray();
        var yGoldNative = rows.Select(r => (double)r.Gold).ToArray();

        // Baseline (no calibration) metrics.
        var (binsBase, eceBase) = MetricsCalculator.CalibrationBins(xRaw, yGoldNorm);
        var maeBase = MetricsCalculator.MeanAbsoluteError(
            xRaw.Select(p => p * 10).ToArray(), yGoldNative);
        var spBase = MetricsCalculator.Spearman(xRaw, yGoldNorm);

        // ── Fit on the full set + report in-sample metrics ─────────────
        _logger.LogInformation("Fitting isotonic regression (PAV)...");
        var isoKnots = IsotonicRegression.Fit(xRaw, yGoldNorm);
        var xIso = xRaw.Select(p => IsotonicRegression.Predict(isoKnots, p)).ToArray();
        var (binsIso, eceIso) = MetricsCalculator.CalibrationBins(xIso, yGoldNorm);
        var maeIso = MetricsCalculator.MeanAbsoluteError(
            xIso.Select(p => p * 10).ToArray(), yGoldNative);
        var spIso = MetricsCalculator.Spearman(xIso, yGoldNorm);

        _logger.LogInformation("Fitting Platt scaling (Newton-Raphson)...");
        var plattParams = PlattScaling.Fit(xRaw, yGoldNorm);
        var xPlatt = xRaw.Select(p => PlattScaling.Predict(plattParams, p)).ToArray();
        var (binsPlatt, ecePlatt) = MetricsCalculator.CalibrationBins(xPlatt, yGoldNorm);
        var maePlatt = MetricsCalculator.MeanAbsoluteError(
            xPlatt.Select(p => p * 10).ToArray(), yGoldNative);
        var spPlatt = MetricsCalculator.Spearman(xPlatt, yGoldNorm);

        // ── 5-fold cross-validated ECE — the honest generalisation number ──
        _logger.LogInformation("Running 5-fold cross-validated ECE...");
        var cvIso = CrossValidateEce(xRaw, yGoldNorm, "isotonic", 5);
        var cvPlatt = CrossValidateEce(xRaw, yGoldNorm, "platt", 5);

        _logger.LogInformation(
            "Baseline ECE={Base:F4} | In-sample iso={Iso:F4} platt={Platt:F4} | 5-fold CV iso={CvIso:F4} platt={CvPlatt:F4}",
            eceBase, eceIso, ecePlatt, cvIso, cvPlatt);

        // Pick the better of the two by CV ECE.
        string chosen = cvIso <= cvPlatt ? "isotonic" : "platt";
        _logger.LogInformation("Chosen calibrator: {Chosen}", chosen);

        // ── Persist calibrator ─────────────────────────────────────────
        var calibratorPath = Path.Combine(outDir, $"calibrator_{version}_{chosen}.json");
        if (chosen == "isotonic")
        {
            var payload = new Dictionary<string, object?>
            {
                ["method"] = "isotonic",
                ["version"] = version + "_isotonic",
                ["fitted_on_n_pairs"] = rows.Count,
                ["fitted_at"] = DateTime.UtcNow.ToString("O"),
                ["knots"] = isoKnots.Select(k => new[] { k.X, k.Y }).ToArray()
            };
            await File.WriteAllTextAsync(calibratorPath,
                JsonSerializer.Serialize(payload, WriteOpts), ct);
        }
        else
        {
            var payload = new Dictionary<string, object?>
            {
                ["method"] = "platt",
                ["version"] = version + "_platt",
                ["fitted_on_n_pairs"] = rows.Count,
                ["fitted_at"] = DateTime.UtcNow.ToString("O"),
                ["a"] = plattParams.A,
                ["b"] = plattParams.B
            };
            await File.WriteAllTextAsync(calibratorPath,
                JsonSerializer.Serialize(payload, WriteOpts), ct);
        }
        _logger.LogInformation("Saved calibrator: {Path}", calibratorPath);

        // ── Markdown report ───────────────────────────────────────────
        var inv = CultureInfo.InvariantCulture;
        var md = new StringBuilder();
        md.AppendLine($"# Calibration report — `{version}`\n");
        md.AppendLine($"N pairs: **{rows.Count}**  ·  Chosen calibrator: **{chosen}**\n");
        md.AppendLine("## Before vs after calibration\n");
        md.AppendLine("| Metric | Before (raw) | Isotonic (in-sample) | Platt (in-sample) | 5-fold CV (chosen) |");
        md.AppendLine("|---|---|---|---|---|");
        md.AppendLine($"| Expected Calibration Error | {eceBase.ToString("F4", inv)} | {eceIso.ToString("F4", inv)} | {ecePlatt.ToString("F4", inv)} | **{(chosen == "isotonic" ? cvIso : cvPlatt).ToString("F4", inv)}** |");
        md.AppendLine($"| MAE (0-10 scale) | {maeBase.ToString("F3", inv)} | {maeIso.ToString("F3", inv)} | {maePlatt.ToString("F3", inv)} | — |");
        md.AppendLine($"| Spearman ρ | {spBase.ToString("F3", inv)} | {spIso.ToString("F3", inv)} | {spPlatt.ToString("F3", inv)} | — |");
        md.AppendLine();
        var ecePct = (1.0 - (chosen == "isotonic" ? cvIso : cvPlatt) / eceBase) * 100;
        md.AppendLine($"**Calibration improvement: ECE {eceBase:F4} → {(chosen == "isotonic" ? cvIso : cvPlatt):F4} ({ecePct:F1}% relative reduction, 5-fold cross-validated)**\n");
        md.AppendLine("Spearman ρ is essentially unchanged by isotonic calibration (a monotonic post-hoc mapping cannot change the rank ordering); the calibration improvement comes entirely from re-mapping the magnitudes to match the gold distribution.\n");

        // Reliability diagram side-by-side
        md.AppendLine("## Reliability diagram\n");
        md.AppendLine("| Bin | n | Raw mean | Iso mean | Platt mean | Mean gold |");
        md.AppendLine("|---|---|---|---|---|---|");
        for (int i = 0; i < binsBase.Count; i++)
        {
            var bRaw = binsBase[i];
            var bIso = binsIso[i];
            var bPlatt = binsPlatt[i];
            if (bRaw.N == 0) {
                md.AppendLine($"| [{bRaw.BinLo:F1}, {bRaw.BinHi:F1}] | 0 | — | — | — | — |");
                continue;
            }
            md.AppendLine($"| [{bRaw.BinLo:F1}, {bRaw.BinHi:F1}] | {bRaw.N} | {bRaw.MeanPredicted:F3} | {bIso.MeanPredicted:F3} | {bPlatt.MeanPredicted:F3} | {bRaw.MeanGold:F3} |");
        }
        md.AppendLine();
        md.AppendLine("## Calibrator artefact\n");
        md.AppendLine($"Saved as `{Path.GetFileName(calibratorPath)}`. The production `RecruiterMonolithicScoringService` can load this JSON and apply the calibration to every raw composite score before returning to the recruiter UI.");

        var reportPath = Path.Combine(outDir, "report.md");
        await File.WriteAllTextAsync(reportPath, md.ToString(), ct);
        _logger.LogInformation("Saved report: {Path}", reportPath);
    }

    /// <summary>Honest generalisation ECE — train on 4 folds, evaluate on 1, average.</summary>
    private static double CrossValidateEce(
        double[] x, double[] y, string method, int folds)
    {
        var indices = Enumerable.Range(0, x.Length).ToList();
        var rng = new Random(42);
        for (int i = indices.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (indices[i], indices[j]) = (indices[j], indices[i]);
        }
        var foldSize = (int)Math.Ceiling(x.Length / (double)folds);
        var eces = new List<double>();
        for (int f = 0; f < folds; f++)
        {
            var test = indices.Skip(f * foldSize).Take(foldSize).ToList();
            var train = indices.Except(test).ToList();
            if (train.Count == 0 || test.Count == 0) continue;
            var xTrain = train.Select(i => x[i]).ToArray();
            var yTrain = train.Select(i => y[i]).ToArray();
            var xTest = test.Select(i => x[i]).ToArray();
            var yTest = test.Select(i => y[i]).ToArray();
            double[] xPredict;
            if (method == "isotonic")
            {
                var knots = IsotonicRegression.Fit(xTrain, yTrain);
                xPredict = xTest.Select(v => IsotonicRegression.Predict(knots, v)).ToArray();
            }
            else
            {
                var p = PlattScaling.Fit(xTrain, yTrain);
                xPredict = xTest.Select(v => PlattScaling.Predict(p, v)).ToArray();
            }
            var (_, ece) = MetricsCalculator.CalibrationBins(xPredict, yTest);
            eces.Add(ece);
        }
        return eces.Count > 0 ? eces.Average() : double.NaN;
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
        double PredictedScore);
}
