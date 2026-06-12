namespace Application.Common.Configuration;


public sealed class ScoringOptions
{

    public const string SectionName = "Scoring";


    public int SyncNormalizeTimeoutSeconds { get; set; } = 300;


    /// <summary>
    /// Which scoring engine the v6 handler should use:
    ///   "linear" — ScoringServiceV2 (seven deterministic C# sub-score calculators)
    ///              plus CompositeJudge anchor + batched reasons (default — production).
    ///   "mono"   — MonolithicScoringService (one Gemini call produces all 7 sub-scores,
    ///              anti-flag penalty, evidence, and bilingual reason in one shot).
    ///              Judge + batched-reason stages are skipped because Mono already
    ///              produces both the composite and the reason text.
    /// </summary>
    public string Engine { get; set; } = "linear";
}
