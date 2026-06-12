using MathNet.Numerics.Statistics;

namespace EvalTool.Metrics;

/// <summary>
/// Pure-statistics helper for thesis evaluation. Computes:
///
/// <list type="bullet">
///   <item><b>Spearman ρ</b> and <b>Kendall τ</b> — rank correlation against gold.</item>
///   <item><b>Quadratic Weighted Kappa (QWK)</b> — standard ordinal-rater agreement.</item>
///   <item><b>MAE</b> on native 0-10 scale.</item>
///   <item><b>NDCG@k</b> — per-CV averaged ranking quality (recruiter perspective).</item>
///   <item><b>Reliability bins + Expected Calibration Error (ECE)</b> —
///         answers "does a predicted 0.65 really mean 65% match quality?".</item>
///   <item><b>Bootstrap 95% CIs</b> — non-parametric, ~1000 resamples.</item>
/// </list>
///
/// No I/O — all inputs are arrays. Used by the <c>compute-metrics</c> CLI command.
/// </summary>
public static class MetricsCalculator
{
    /// <summary>Spearman rank correlation in [-1, 1].</summary>
    public static double Spearman(double[] x, double[] y) =>
        Correlation.Spearman(x, y);

    /// <summary>Kendall τ rank correlation in [-1, 1].</summary>
    public static double KendallTau(double[] x, double[] y)
    {
        // MathNet provides Pearson/Spearman; Kendall implemented locally
        // O(n²) — fine for n ≤ a few thousand.
        var n = x.Length;
        long concordant = 0, discordant = 0, tiesX = 0, tiesY = 0;
        for (var i = 0; i < n; i++)
        for (var j = i + 1; j < n; j++)
        {
            var dx = x[i] - x[j];
            var dy = y[i] - y[j];
            var prod = dx * dy;
            if (prod > 0) concordant++;
            else if (prod < 0) discordant++;
            else
            {
                if (dx == 0) tiesX++;
                if (dy == 0) tiesY++;
            }
        }
        var nPairs = (long)n * (n - 1) / 2;
        var denom = Math.Sqrt((double)(nPairs - tiesX) * (nPairs - tiesY));
        return denom == 0 ? 0.0 : (concordant - discordant) / denom;
    }

    /// <summary>
    /// Quadratic Weighted Kappa on integer ordinal scale [min, max].
    /// Standard metric for ordinal rater agreement (penalises bigger
    /// disagreements more — Δ=4 four times worse than Δ=2).
    /// </summary>
    public static double QuadraticWeightedKappa(
        int[] yTrue, int[] yPred, int min = 0, int max = 10)
    {
        var R = max - min + 1;
        var cm = new double[R, R];
        for (var i = 0; i < yTrue.Length; i++)
        {
            var t = Math.Clamp(yTrue[i] - min, 0, R - 1);
            var p = Math.Clamp(yPred[i] - min, 0, R - 1);
            cm[t, p]++;
        }
        var W = new double[R, R];
        for (var i = 0; i < R; i++)
        for (var j = 0; j < R; j++)
            W[i, j] = ((i - j) * (i - j)) / (double)((R - 1) * (R - 1));

        var histT = new double[R];
        var histP = new double[R];
        double total = 0;
        for (var i = 0; i < R; i++)
        for (var j = 0; j < R; j++)
        {
            histT[i] += cm[i, j];
            histP[j] += cm[i, j];
            total += cm[i, j];
        }

        double num = 0, den = 0;
        for (var i = 0; i < R; i++)
        for (var j = 0; j < R; j++)
        {
            var e = histT[i] * histP[j] / total;
            num += W[i, j] * cm[i, j];
            den += W[i, j] * e;
        }
        return den == 0 ? 0.0 : 1.0 - num / den;
    }

    /// <summary>Mean absolute error on the native scale (e.g., 0-10).</summary>
    public static double MeanAbsoluteError(double[] predicted, double[] gold)
    {
        double sum = 0;
        for (var i = 0; i < predicted.Length; i++)
            sum += Math.Abs(predicted[i] - gold[i]);
        return sum / predicted.Length;
    }

    /// <summary>
    /// NDCG@k averaged across query groups (CVs). Each group's ranking is
    /// sorted by descending predicted score; relevance = the gold rating.
    /// </summary>
    public static double NdcgAtK(
        IEnumerable<(string GroupId, double Predicted, double Gold)> pairs, int k)
    {
        var grouped = pairs.GroupBy(p => p.GroupId);
        var ndcgs = new List<double>();
        foreach (var g in grouped)
        {
            var sorted = g.OrderByDescending(p => p.Predicted).Take(k).ToList();
            if (sorted.Count < 2) continue;
            var dcg = Dcg(sorted.Select(p => p.Gold).ToArray());
            var ideal = sorted.Select(p => p.Gold).OrderByDescending(g => g).ToArray();
            var idcg = Dcg(ideal);
            ndcgs.Add(idcg > 0 ? dcg / idcg : 0.0);
        }
        return ndcgs.Count == 0 ? 0.0 : ndcgs.Average();
    }

    private static double Dcg(double[] rels)
    {
        double s = 0;
        for (var i = 0; i < rels.Length; i++)
            s += rels[i] / Math.Log2(i + 2);
        return s;
    }

    /// <summary>
    /// 10-bin reliability diagram + Expected Calibration Error.
    /// Both predicted and gold must be in [0, 1].
    /// </summary>
    public static (List<CalibrationBin> Bins, double Ece) CalibrationBins(
        double[] predictedNorm, double[] goldNorm, int nBins = 10)
    {
        var bins = new List<CalibrationBin>();
        var N = predictedNorm.Length;
        double ece = 0;
        for (var b = 0; b < nBins; b++)
        {
            var lo = b / (double)nBins;
            var hi = (b + 1) / (double)nBins;
            var inBin = new List<int>();
            for (var i = 0; i < N; i++)
            {
                var p = predictedNorm[i];
                var inRange = (p >= lo) && (b == nBins - 1 ? p <= hi : p < hi);
                if (inRange) inBin.Add(i);
            }
            if (inBin.Count == 0)
            {
                bins.Add(new CalibrationBin(lo, hi, 0, null, null, null));
                continue;
            }
            var mp = inBin.Select(i => predictedNorm[i]).Average();
            var mt = inBin.Select(i => goldNorm[i]).Average();
            var gap = Math.Abs(mp - mt);
            ece += (inBin.Count / (double)N) * gap;
            bins.Add(new CalibrationBin(lo, hi, inBin.Count, mp, mt, gap));
        }
        return (bins, ece);
    }

    /// <summary>
    /// Non-parametric 95% CI via bootstrap (1000 resamples by default).
    /// </summary>
    public static (double Low, double High) BootstrapCi(
        Func<double[], double[], double> stat,
        double[] x, double[] y,
        int nResamples = 1000, double ciPct = 95, int seed = 42)
    {
        if (x.Length != y.Length || x.Length == 0) return (double.NaN, double.NaN);
        var rng = new Random(seed);
        var stats = new List<double>(nResamples);
        var N = x.Length;
        var bx = new double[N];
        var by = new double[N];
        for (var r = 0; r < nResamples; r++)
        {
            for (var i = 0; i < N; i++)
            {
                var idx = rng.Next(N);
                bx[i] = x[idx];
                by[i] = y[idx];
            }
            try { stats.Add(stat(bx, by)); } catch { /* skip degenerate samples */ }
        }
        if (stats.Count == 0) return (double.NaN, double.NaN);
        stats.Sort();
        var loPct = (100 - ciPct) / 2.0 / 100.0;
        var hiPct = 1.0 - loPct;
        var loIdx = (int)Math.Floor(loPct * stats.Count);
        var hiIdx = (int)Math.Floor(hiPct * stats.Count);
        return (stats[loIdx], stats[Math.Min(hiIdx, stats.Count - 1)]);
    }

    /// <summary>
    /// Quantise predicted [0,1] score to the nearest ordinal anchor
    /// {0, 2, 4, 6, 8, 10} for QWK.
    /// </summary>
    public static int QuantiseToAnchor(double scoreNorm)
    {
        var raw = scoreNorm * 10.0;
        var snapped = (int)Math.Round(raw / 2.0) * 2;
        return Math.Clamp(snapped, 0, 10);
    }

    public sealed record CalibrationBin(
        double BinLo,
        double BinHi,
        int N,
        double? MeanPredicted,
        double? MeanGold,
        double? AbsGap);
}
