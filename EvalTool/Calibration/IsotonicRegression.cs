namespace EvalTool.Calibration;

/// <summary>
/// Pool-Adjacent-Violators (PAV) isotonic regression. Standard non-parametric
/// calibrator that fits a monotonic non-decreasing function
/// <c>g(x)</c> minimising squared error to the gold labels, subject to the
/// constraint <c>g(x_i) ≤ g(x_{i+1})</c> whenever <c>x_i ≤ x_{i+1}</c>.
///
/// Use as a thin compatibility shim: train via <see cref="Fit"/>, persist
/// the produced knots, then call <see cref="Predict"/> at runtime to map
/// any raw score to its calibrated value. The runtime predict is a linear
/// interpolation between adjacent knots (clamped at endpoints) so the
/// calibrator behaves smoothly outside the training distribution.
/// </summary>
public static class IsotonicRegression
{
    /// <summary>
    /// Fit a monotonic mapping from raw scores <paramref name="x"/> to
    /// gold labels <paramref name="y"/>. Returns a sorted list of
    /// <c>(rawScore, calibratedScore)</c> knots ready for runtime
    /// interpolation by <see cref="Predict"/>.
    /// </summary>
    public static List<(double X, double Y)> Fit(double[] x, double[] y)
    {
        if (x.Length != y.Length) throw new ArgumentException("x and y must have equal length");
        if (x.Length == 0) return new();

        // Sort by x and apply PAV in O(n).
        var idx = Enumerable.Range(0, x.Length).OrderBy(i => x[i]).ToArray();
        var xs = idx.Select(i => x[i]).ToArray();
        var ys = idx.Select(i => y[i]).ToArray();

        var values = new double[xs.Length];
        var weights = new double[xs.Length];
        var starts = new int[xs.Length];
        int blockCount = 0;

        for (int i = 0; i < xs.Length; i++)
        {
            values[blockCount] = ys[i];
            weights[blockCount] = 1.0;
            starts[blockCount] = i;
            blockCount++;

            // Pool down with previous block while the monotonic constraint is violated.
            while (blockCount > 1 && values[blockCount - 2] > values[blockCount - 1])
            {
                double newVal = (values[blockCount - 2] * weights[blockCount - 2]
                               + values[blockCount - 1] * weights[blockCount - 1])
                              / (weights[blockCount - 2] + weights[blockCount - 1]);
                weights[blockCount - 2] += weights[blockCount - 1];
                values[blockCount - 2] = newVal;
                blockCount--;
            }
        }

        // Reduce each block to a single (x, y) knot — the block's mean x and pooled y.
        var knots = new List<(double X, double Y)>(blockCount);
        for (int b = 0; b < blockCount; b++)
        {
            int blockStart = starts[b];
            int blockEnd = b + 1 < blockCount ? starts[b + 1] : xs.Length;
            double meanX = 0;
            for (int j = blockStart; j < blockEnd; j++) meanX += xs[j];
            meanX /= (blockEnd - blockStart);
            knots.Add((meanX, values[b]));
        }

        // Deduplicate identical x's keeping the last (highest y) and resort.
        knots = knots.OrderBy(k => k.X).ToList();
        return knots;
    }

    /// <summary>
    /// Linearly interpolate between knots. Below the smallest knot's x
    /// returns the smallest knot's y; above the largest x returns the
    /// largest knot's y (clamped). Identical to <c>scipy.interpolate.interp1d</c>
    /// with kind='linear' and clamp behaviour.
    /// </summary>
    public static double Predict(IReadOnlyList<(double X, double Y)> knots, double x)
    {
        if (knots.Count == 0) return x;
        if (knots.Count == 1) return knots[0].Y;
        if (x <= knots[0].X) return knots[0].Y;
        if (x >= knots[^1].X) return knots[^1].Y;

        // Binary search the segment that contains x.
        int lo = 0, hi = knots.Count - 1;
        while (hi - lo > 1)
        {
            int mid = (lo + hi) / 2;
            if (knots[mid].X <= x) lo = mid;
            else hi = mid;
        }
        var (x0, y0) = knots[lo];
        var (x1, y1) = knots[hi];
        if (x1 == x0) return y0;
        var t = (x - x0) / (x1 - x0);
        return y0 + t * (y1 - y0);
    }
}
