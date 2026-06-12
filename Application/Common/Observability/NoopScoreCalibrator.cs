using Application.Common.Interfaces;

namespace Application.Common.Observability;

/// <summary>
/// Pass-through calibrator. Returned by <see cref="CalibratorLoader"/>
/// when no calibrator file is configured or when loading the configured
/// file fails. Keeps production code paths identical regardless of
/// whether calibration is enabled — the scoring service can always
/// call <c>_calibrator.Calibrate(score)</c> without an enable-check.
/// </summary>
public sealed class NoopScoreCalibrator : IScoreCalibrator
{
    public static readonly NoopScoreCalibrator Instance = new();

    public string Version => "noop";
    public bool IsEnabled => false;
    public double Calibrate(double rawScore) => rawScore;
}
