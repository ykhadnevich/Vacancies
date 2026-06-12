using Domain.Scoring;

namespace Application.Common.Scoring;


/// <summary>
/// Safety net for LLM-based scoring (Mono): expands variance of a result set when the
/// scorer collapses many real differences onto a single round number (typical LLM
/// failure mode — 35 vacancies all at 0.88).
///
/// Algorithm (z-score rescale around the cohort mean):
///     mean    = average of scores in the cohort
///     std     = standard deviation of scores
///     if std < <see cref="MinObservedStd"/>:
///         scale  = TargetStd / max(std, Epsilon)
///         score' = mean + (score - mean) * scale, clamped to [Floor, Ceiling]
///
/// Ranks are strictly preserved (monotonic transform). Mean is preserved. Verdict
/// boundaries should be recomputed downstream because the *value* changed.
/// </summary>
public static class ScoreDispersion
{

    public const double TargetStd      = 0.15;
    public const double MinObservedStd = 0.05;
    public const double Floor          = 0.05;
    public const double Ceiling        = 0.98;
    public const int    MinCohortSize  = 5;


    public const int    TopClusterWindow    = 20;
    public const double TopClusterThreshold = 0.015;


    private const double Epsilon       = 1e-6;


    public const double TopClusterEpsilon  = 0.001;
    public const double TopClusterStepSize = 0.008;
    public const double TopClusterMinSpan  = 0.15;
    public const int    TopClusterMinSize  = 3;


    public const double TargetTopMax = 0.95;
    public const double LiftEpsilon  = 0.001;


    /// <summary>
    /// Applies dispersion to a cohort. Two stages:
    ///   1) Find the cluster of entries whose score is within
    ///      <see cref="TopClusterEpsilon"/> of the maximum (i.e. all the
    ///      Mono "0.880 group"). When the cluster has ≥ <see cref="TopClusterMinSize"/>
    ///      members, rewrite their scores with a uniform descending spread.
    ///      Step size scales with cluster size so a 30-item cluster spreads
    ///      across ≥0.24, not the fixed 0.15. Tie-breaker = original index
    ///      order (in the v6 handler that is publish-date desc).
    ///   2) If overall std is still below <see cref="MinObservedStd"/>, run a
    ///      z-score rescale around the cohort mean toward <see cref="TargetStd"/>.
    /// Final scores are clamped to [<see cref="Floor"/>, <see cref="Ceiling"/>].
    /// </summary>
    public static IReadOnlyList<double> Apply(IReadOnlyList<double> scores)
    {
        if (scores.Count < MinCohortSize) return scores;

        var working = scores.ToArray();


        double topMax = working.Max();
        var clusterIndices = working
            .Select((s, i) => (Score: s, Index: i))
            .Where(t => t.Score >= topMax - TopClusterEpsilon)
            .OrderBy(t => t.Index)
            .Select(t => t.Index)
            .ToList();

        if (clusterIndices.Count >= TopClusterMinSize)
        {
            int n  = clusterIndices.Count;
            double span = Math.Max(TopClusterMinSpan, TopClusterStepSize * (n - 1));
            double step = span / (n - 1);
            for (int rank = 0; rank < n; rank++)
            {
                working[clusterIndices[rank]] = topMax - rank * step;
            }
        }


        double mean = 0;
        foreach (var s in working) mean += s;
        mean /= working.Length;

        double variance = 0;
        foreach (var s in working) variance += (s - mean) * (s - mean);
        variance /= working.Length;
        double std = Math.Sqrt(variance);

        if (std < MinObservedStd)
        {
            double scale = TargetStd / Math.Max(std, Epsilon);
            for (int i = 0; i < working.Length; i++)
            {
                working[i] = mean + (working[i] - mean) * scale;
            }
        }

        // Stage 3 — Top-stretch. LLMs anchor "high score" around 0.85-0.90 and
        // almost never emit 1.00, so even an ideal CV-vacancy pair plateaus
        // at ~0.88 composite. If the current cohort top is below TargetTopMax,
        // multiplicatively scale every score so the new top = TargetTopMax.
        // Ratios are preserved (rank order intact); the bottom of the cohort
        // moves up proportionally, then gets clamped at Ceiling=0.98.
        double cohortMax = working.Max();
        if (cohortMax > LiftEpsilon && cohortMax < TargetTopMax)
        {
            double factor = TargetTopMax / cohortMax;
            for (int i = 0; i < working.Length; i++)
                working[i] *= factor;
        }

        for (int i = 0; i < working.Length; i++)
            working[i] = Math.Clamp(working[i], Floor, Ceiling);
        return working;
    }


    public static double ComputeStd(IReadOnlyList<double> scores)
    {
        if (scores.Count == 0) return 0;
        double mean = 0;
        foreach (var s in scores) mean += s;
        mean /= scores.Count;

        double variance = 0;
        foreach (var s in scores) variance += (s - mean) * (s - mean);
        return Math.Sqrt(variance / scores.Count);
    }


    /// <summary>
    /// Returns the std of the top-<paramref name="windowSize"/> scores after sorting desc.
    /// Used to detect tight top-of-distribution clusters that the overall std hides —
    /// the typical Mono failure mode (35 vacancies all at 0.88 while a long lower tail
    /// keeps total std looking healthy).
    /// </summary>
    public static double ComputeTopStd(IReadOnlyList<double> scores, int windowSize)
    {
        if (scores.Count == 0) return 0;
        var top = scores.OrderByDescending(s => s).Take(windowSize).ToList();
        return ComputeStd(top);
    }


    /// <summary>
    /// Combined trigger: returns true when either (a) the whole cohort is collapsed
    /// (overall std &lt; <see cref="MinObservedStd"/>) or (b) only the top portion
    /// is glued (top-<paramref name="topWindow"/> std &lt; <see cref="TopClusterThreshold"/>).
    /// </summary>
    public static bool ShouldApply(
        IReadOnlyList<double> scores, int topWindow, out string reason)
    {
        var overallStd = ComputeStd(scores);
        if (overallStd < MinObservedStd)
        {
            reason = $"overall std {overallStd:F3} < {MinObservedStd:F3}";
            return true;
        }
        var topStd = ComputeTopStd(scores, topWindow);
        if (topStd < TopClusterThreshold)
        {
            reason = $"top-{topWindow} std {topStd:F3} < {TopClusterThreshold:F3} (tight top cluster)";
            return true;
        }
        reason = $"overall std {overallStd:F3}, top-{topWindow} std {topStd:F3} — both healthy";
        return false;
    }
}
