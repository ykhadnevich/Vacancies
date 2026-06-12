using Application.Common.Interfaces;

namespace Infrastructure.Calibration;

/// <summary>
/// Runtime Platt scaling calibrator. Applies the sigmoid
/// <c>p = 1 / (1 + exp(A · x + B))</c> with parameters fitted by the
/// EvalTool <c>fit-calibration</c> command. Two floating-point
/// multiplies + one transcendental per call — sub-microsecond.
/// </summary>
public sealed class PlattCalibratorRuntime : IScoreCalibrator
{
    private readonly double _a;
    private readonly double _b;

    public string Version { get; }
    public bool IsEnabled => true;

    public PlattCalibratorRuntime(string version, double a, double b)
    {
        Version = version;
        _a = a;
        _b = b;
    }

    public double Calibrate(double rawScore)
    {
        // Defensive NaN guard — see IsotonicCalibratorRuntime for the rationale.
        // 0.5 is the sigmoid's midpoint; safer than emitting NaN on the hot path.
        if (double.IsNaN(rawScore)) return 0.5;
        double z = _a * rawScore + _b;
        if (z >= 0)
        {
            double e = Math.Exp(-z);
            return 1.0 / (1.0 + e);
        }
        else
        {
            double e = Math.Exp(z);
            return e / (1.0 + e);
        }
    }
}
