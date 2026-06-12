namespace Domain.Scoring;


/// <summary>
/// Single source of truth for empirically tuned calibration constants used by
/// the Linear scoring pipeline and its supporting evaluators.
///
/// Each nested class groups constants by their semantic role. Constants are
/// versioned through <see cref="CalibrationVersion"/> so downstream eval reports
/// can attribute scores to the exact calibration that produced them.
///
/// Note: this class deliberately excludes <em>semantic ladders</em> that encode
/// external standards (CEFR levels, Bologna degree ranks, the seniority
/// mismatch matrix). Those are <em>data</em>, not hyperparameters, and remain
/// inside their respective calculators.
/// </summary>
public static class ScoringConstants
{

    public const string CalibrationVersion = "linear_v6.1";


    /// <summary>
    /// Weighted contribution of each sub-axis to the linear composite score.
    /// Must sum to 1.0 — verified by <c>ScoringConstantsTests</c>.
    /// </summary>
    public static class LinearWeights
    {
        public const double Skill        = 0.40;
        public const double Seniority    = 0.15;
        public const double Experience   = 0.15;
        public const double RoleIntent   = 0.15;
        public const double Domain       = 0.08;
        public const double Language     = 0.05;
        public const double Education    = 0.02;
    }


    /// <summary>
    /// Multiplicative penalty applied to the weighted sum based on the number
    /// of triggered anti-flags. Must be monotonically non-increasing.
    /// </summary>
    public static class AntiFlag
    {
        public const double PenaltyNone = 1.0;
        public const double PenaltyOne  = 0.5;
        public const double PenaltyMany = 0.2;
    }


    /// <summary>
    /// Family-aware floors for the domain alignment sub-score.
    ///
    /// <c>EmptyXxx</c> applies when the vacancy has no <c>domain_context</c> signal
    /// (or it is the catch-all "other"). <c>MatchXxx</c> applies when partial overlap
    /// exists — final = floor + (1 - floor) * jaccard.
    ///
    /// Rationale: engineering / DevOps / data candidates transition between
    /// industries cheaply (low floor), product / design / marketing rely on
    /// domain knowledge as a load-bearing signal (soft floor), the remaining
    /// families fall back to a generic mid-range floor.
    /// </summary>
    public static class DomainFloors
    {
        public const double EmptyTech         = 0.0;
        public const double EmptyDomainHeavy  = 0.5;
        public const double EmptyDefault      = 0.7;
        public const double MatchTech         = 0.0;
        public const double MatchDomainHeavy  = 0.3;
        public const double MatchDefault      = 0.5;
    }


    /// <summary>
    /// Calibration for the role-intent sub-score: how the stated intent
    /// (target_roles) and revealed intent (experience job titles) are weighted
    /// and how raw Jaccard similarity is mapped to a sub-score.
    /// </summary>
    public static class RoleIntent
    {

        public const double FallbackWeight    = 0.7;


        public const double JaccardHigh       = 0.66;
        public const double JaccardMid        = 0.33;


        public const double ScoreEmptyTitle   = 0.5;
        public const double ScoreHigh         = 1.0;
        public const double ScoreMid          = 0.85;
        public const double ScoreLow          = 0.6;
        public const double ScoreNone         = 0.0;
    }


    /// <summary>
    /// Calibration for skill matching.
    /// </summary>
    public static class SkillMatch
    {

        public const double NiceToHaveBonus     = 0.30;


        public const double ExpansionThreshold  = 0.30;
    }


    /// <summary>
    /// Extreme bands used by ScoringServiceV2 to decide whether to invoke the
    /// Composite Judge or trust the linear anchor outright.
    /// </summary>
    public static class ExtremeBand
    {
        public const double Low  = 0.30;
        public const double High = 0.85;
    }
}
