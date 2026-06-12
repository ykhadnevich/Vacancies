namespace Application.Common.Interfaces;

/// <summary>
/// Maps a raw scoring-service composite (the weighted sum of sub-scores
/// times anti-flag penalty, clamped to [0, 1]) onto a calibrated value
/// whose percentage interpretation matches the gold distribution.
///
/// Resolves the systematic over-confidence finding from the held-out
/// evaluation (Layer 6): the raw composite over-reports by 9–18
/// percentage points across the full distribution. A post-hoc isotonic
/// regression or Platt scaling calibrator — fitted on the held-out gold
/// via <c>EvalTool fit-calibration</c> — corrects this bias before the
/// recruiter UI sees the number.
///
/// Implementations must be thread-safe (singleton at runtime) and must
/// not throw from <see cref="Calibrate"/> — production hot path. The
/// default implementation <c>NoopCalibrator</c> simply returns the
/// input unchanged and is used when no calibrator file is configured.
/// </summary>
public interface IScoreCalibrator
{
    /// <summary>Identifier of the calibrator artefact in use, for logging and result attribution.</summary>
    string Version { get; }

    /// <summary>True when a real calibrator is loaded; false for the no-op fallback.</summary>
    bool IsEnabled { get; }

    /// <summary>Apply the calibration mapping to a raw composite score in [0, 1].</summary>
    double Calibrate(double rawScore);
}
