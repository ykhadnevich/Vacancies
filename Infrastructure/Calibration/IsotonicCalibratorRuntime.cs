using Application.Common.Interfaces;

namespace Infrastructure.Calibration;

/// <summary>
/// Runtime isotonic-regression calibrator. Loads a list of <c>(x, y)</c>
/// knots (sorted by x) produced by the offline EvalTool
/// <c>fit-calibration</c> command and linearly interpolates between them
/// at scoring time.
///
/// Behaviour at the endpoints is clamping: a raw score below the
/// smallest knot's x returns the smallest knot's y; above the largest
/// returns the largest's y. Identical to
/// <c>scipy.interpolate.interp1d(kind='linear', bounds_error=False, fill_value=(...))</c>.
///
/// Thread-safe: instance state is read-only after construction. The
/// <c>Calibrate</c> method performs a binary search over the knots
/// (<c>O(log n)</c> with n ≈ 20–100 for typical isotonic fits) plus
/// one linear interpolation — sub-microsecond per call.
/// </summary>
public sealed class IsotonicCalibratorRuntime : IScoreCalibrator
{
    private readonly (double X, double Y)[] _knots;

    public string Version { get; }
    public bool IsEnabled => true;

    public IsotonicCalibratorRuntime(string version, IReadOnlyList<(double X, double Y)> knots)
    {
        Version = version;
        if (knots is null || knots.Count == 0)
            throw new ArgumentException("Isotonic calibrator requires at least one knot", nameof(knots));
        // Defensive sort — we trust the file but pay the O(n log n) once at startup.
        _knots = knots.OrderBy(k => k.X).ToArray();
    }

    public double Calibrate(double rawScore)
    {
        // Defensive: a NaN raw composite from upstream (rare but possible if a
        // sub-score parser silently fails) would otherwise propagate through
        // the binary search (`NaN <= x` and `NaN >= x` are both false) and
        // emerge as a NaN in the API response. Return the lowest knot — safer
        // than NaN-poisoning the recruiter UI.
        if (double.IsNaN(rawScore)) return _knots[0].Y;
        if (_knots.Length == 1) return _knots[0].Y;
        if (rawScore <= _knots[0].X) return _knots[0].Y;
        if (rawScore >= _knots[^1].X) return _knots[^1].Y;

        int lo = 0, hi = _knots.Length - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (_knots[mid].X <= rawScore) lo = mid;
            else hi = mid;
        }
        var (x0, y0) = _knots[lo];
        var (x1, y1) = _knots[hi];
        if (x1 == x0) return y0;
        var t = (rawScore - x0) / (x1 - x0);
        return y0 + t * (y1 - y0);
    }
}
