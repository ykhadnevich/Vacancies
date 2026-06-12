namespace EvalTool.Calibration;

/// <summary>
/// Platt scaling — a parametric calibration that fits a sigmoid
/// <c>p(y=1 | x) = 1 / (1 + exp(A · x + B))</c> via maximum-likelihood
/// estimation. For ordinal calibration we treat gold ∈ [0, 1] as a soft
/// probability and minimise the binary cross-entropy.
///
/// Compared to isotonic regression, Platt has two parameters only (A, B)
/// and is therefore robust on smaller calibration sets but assumes
/// sigmoid-shaped miscalibration. The held-out evaluation should test
/// both and persist whichever achieves lower ECE on cross-validation.
/// </summary>
public static class PlattScaling
{
    public sealed record Result(double A, double B);

    /// <summary>
    /// Fit (A, B) by maximising log-likelihood with Newton-Raphson on a
    /// soft-label binary cross-entropy. Stopping criterion: gradient norm
    /// less than 1e-7 or 200 iterations.
    /// </summary>
    public static Result Fit(double[] x, double[] yGoldNorm)
    {
        if (x.Length != yGoldNorm.Length) throw new ArgumentException("length mismatch");
        if (x.Length == 0) return new Result(0, 0);

        // Smooth the targets (Platt 1999 recommendation) — keeps gradients finite
        // when the gold is exactly 0 or 1. Soft labels in [0, 1].
        int n = x.Length;
        double[] t = new double[n];
        for (int i = 0; i < n; i++)
            t[i] = (yGoldNorm[i] + 0.5 / (n + 2)) / (1.0 + 1.0 / (n + 2));

        double a = 0, b = Math.Log((1 - t.Average()) / Math.Max(t.Average(), 1e-9));
        const double tol = 1e-7;
        const int maxIter = 200;

        for (int it = 0; it < maxIter; it++)
        {
            double g1 = 0, g2 = 0, h11 = 0, h22 = 0, h21 = 0;
            for (int i = 0; i < n; i++)
            {
                double fApb = a * x[i] + b;
                double p, q;
                if (fApb >= 0)
                {
                    double e = Math.Exp(-fApb);
                    p = e / (1 + e);
                    q = 1 / (1 + e);
                }
                else
                {
                    double e = Math.Exp(fApb);
                    p = 1 / (1 + e);
                    q = e / (1 + e);
                }
                double d2 = p * q;
                double d1 = t[i] - p;
                g1 += x[i] * d1;
                g2 += d1;
                h11 += x[i] * x[i] * d2;
                h22 += d2;
                h21 += x[i] * d2;
            }
            if (Math.Abs(g1) < tol && Math.Abs(g2) < tol) break;
            // Solve 2x2 Hessian.
            double det = h11 * h22 - h21 * h21;
            if (Math.Abs(det) < 1e-18) break;
            double da = -(h22 * g1 - h21 * g2) / det;
            double db = -(-h21 * g1 + h11 * g2) / det;
            a += da; b += db;
        }
        // Flip sign convention to match the spec p = 1 / (1 + exp(A·x + B)).
        return new Result(-a, -b);
    }

    /// <summary>Apply the fitted sigmoid: <c>p = 1 / (1 + exp(A·x + B))</c>.</summary>
    public static double Predict(Result r, double x)
    {
        double z = r.A * x + r.B;
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
