using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Observability;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Calibration;

/// <summary>
/// One-time loader that reads a calibrator artefact (the JSON file
/// produced by <c>EvalTool fit-calibration</c>) and returns the
/// appropriate runtime calibrator. Falls back to <see cref="NoopScoreCalibrator"/>
/// when the file is absent, unreadable, or contains an unrecognised method.
///
/// The fallback is intentional: a missing calibrator must not crash the
/// scoring service. A warning is logged so an operator can investigate,
/// but production scoring continues to emit raw (un-calibrated) scores —
/// the previous production behaviour.
/// </summary>
public static class CalibratorLoader
{
    public static IScoreCalibrator LoadOrNoop(string? path, ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            logger.LogInformation(
                "Score calibrator not configured (Calibration:RecruiterPath is empty) — using NoopScoreCalibrator");
            return NoopScoreCalibrator.Instance;
        }
        if (!File.Exists(path))
        {
            logger.LogWarning(
                "Score calibrator file not found at {Path} — using NoopScoreCalibrator. " +
                "Run 'dotnet run --project EvalTool -- fit-calibration' to produce one.",
                path);
            return NoopScoreCalibrator.Instance;
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var root = doc.RootElement;

            if (!root.TryGetProperty("method", out var methodEl)
                || methodEl.ValueKind != JsonValueKind.String)
            {
                logger.LogWarning(
                    "Calibrator file at {Path} is missing a 'method' field — using NoopScoreCalibrator",
                    path);
                return NoopScoreCalibrator.Instance;
            }
            var method = methodEl.GetString()!;
            var version = root.TryGetProperty("version", out var vEl) && vEl.ValueKind == JsonValueKind.String
                ? vEl.GetString()! : "unknown";

            switch (method)
            {
                case "isotonic":
                {
                    if (!root.TryGetProperty("knots", out var knotsEl)
                        || knotsEl.ValueKind != JsonValueKind.Array)
                    {
                        logger.LogWarning(
                            "Isotonic calibrator at {Path} is missing 'knots' array — using NoopScoreCalibrator",
                            path);
                        return NoopScoreCalibrator.Instance;
                    }
                    var knots = new List<(double, double)>();
                    foreach (var pair in knotsEl.EnumerateArray())
                    {
                        if (pair.ValueKind != JsonValueKind.Array || pair.GetArrayLength() != 2) continue;
                        knots.Add((pair[0].GetDouble(), pair[1].GetDouble()));
                    }
                    if (knots.Count == 0)
                    {
                        logger.LogWarning(
                            "Isotonic calibrator at {Path} has zero knots — using NoopScoreCalibrator",
                            path);
                        return NoopScoreCalibrator.Instance;
                    }
                    logger.LogInformation(
                        "Loaded isotonic calibrator: version={Version}, knots={N}, path={Path}",
                        version, knots.Count, path);
                    return new IsotonicCalibratorRuntime(version, knots);
                }
                case "platt":
                {
                    if (!root.TryGetProperty("a", out var aEl) || !root.TryGetProperty("b", out var bEl))
                    {
                        logger.LogWarning(
                            "Platt calibrator at {Path} is missing 'a' or 'b' — using NoopScoreCalibrator",
                            path);
                        return NoopScoreCalibrator.Instance;
                    }
                    var a = aEl.GetDouble();
                    var b = bEl.GetDouble();
                    logger.LogInformation(
                        "Loaded Platt calibrator: version={Version}, A={A}, B={B}, path={Path}",
                        version, a, b, path);
                    return new PlattCalibratorRuntime(version, a, b);
                }
                default:
                    logger.LogWarning(
                        "Unknown calibrator method '{Method}' in {Path} — using NoopScoreCalibrator",
                        method, path);
                    return NoopScoreCalibrator.Instance;
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load calibrator from {Path} — using NoopScoreCalibrator (production scoring continues with raw scores)",
                path);
            return NoopScoreCalibrator.Instance;
        }
    }
}
