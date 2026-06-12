namespace Domain.Scoring;


public sealed record ScoringResult(
    Guid VacancyId,
    string CvId,
    string ModelVersion,
    DateTime GeneratedAt,

    double Score,
    SubScores SubScores,
    double AntiFlagPenalty,

    string ReasonEn,
    string? ReasonUk,

    ScoringEvidence Evidence,


    int InputTokens = 0,
    int OutputTokens = 0,


    string? StrengthsEn = null,
    string? StrengthsUk = null,
    string? GapsEn = null,
    string? GapsUk = null,
    string? RecommendationEn = null,
    string? RecommendationUk = null,


    Verdict Verdict = Verdict.Mismatch,
    ReasonContext? Context = null,


    /// <summary>
    /// LLM self-reported confidence in [0,1] for the produced sub_scores.
    /// 1.0 means "evidence in the inputs is unambiguous and the score is well-grounded".
    /// Lower values warn the caller that the inputs were sparse / contradictory
    /// and that the final score should be treated with caution.
    /// 1.0 by default so deterministic services (Linear) trivially conform.
    /// </summary>
    double Confidence = 1.0);


public sealed record ScoringEvidence(
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingMustHaves,
    IReadOnlyList<string> TriggeredAntiFlags);


public sealed record ReasonContext(
    int? CandidateYearsOfExperience,
    int? VacancyRequiredYears,
    int? OverqualifiedByYears,
    int? UnderqualifiedByYears,
    string? CandidateSeniority,
    string? VacancySeniority,
    IReadOnlyList<string> CandidateTargetRoles,
    string? VacancyRoleEn,
    bool TargetRoleAligned,
    bool CrossDomainTransition,
    string? CandidateDomainsSummary,
    string? VacancyDomain,


    string? CandidateEnglishLevel = null,
    string? VacancyEnglishRequired = null);
