namespace Application.DTOs.Recruiter;

public sealed record RecruiterVacancyDto(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    string? Description,
    DateTime CreatedAt,
    bool IsNormalized,
    int ScoredCandidatesCount);

public sealed record CandidateListDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    int TotalCandidates,
    int NormalizedCandidates,
    int FailedCandidates);

public sealed record CandidateInListDto(
    Guid Id,
    string? CandidateName,
    string NormalizationStatus,
    string? LastError,
    DateTime AddedAt);

public sealed record CandidateAnalysisResultDto(
    Guid CandidateId,
    string? CandidateName,
    double Score,
    string Verdict,
    string? ReasonUk,
    string? ReasonEn,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingMustHaves,
    IReadOnlyList<string> TriggeredAntiFlags,
    IReadOnlyDictionary<string, double> SubScores,
    double AntiFlagPenalty,
    double? Confidence,
    int InputTokens,
    int OutputTokens,
    double EstimatedCostUsd,
    string ModelVersion,
    DateTime ScoredAt);
