using Domain.Entities;

namespace Domain.Interfaces.Repositories;


public interface IScoringCacheRepository
{


    Task<IReadOnlyDictionary<Guid, ScoringCacheEntry>> GetForCvAsync(
        string cvHash,
        IReadOnlyCollection<Guid> vacancyIds,
        string scoringVersion,
        CancellationToken ct = default);


    Task UpsertJudgeBatchAsync(
        string cvHash,
        string scoringVersion,
        IReadOnlyList<JudgeCacheUpsert> entries,
        CancellationToken ct = default);


    Task UpsertReasonBatchAsync(
        string cvHash,
        string scoringVersion,
        IReadOnlyList<ReasonCacheUpsert> entries,
        CancellationToken ct = default);


    /// <summary>
    /// Persists Mono-engine results in batch. Each entry stores the full
    /// serialised <see cref="Domain.Scoring.ScoringResult"/> for the given
    /// (cvHash, vacancyId, scoringVersion) key. Used by the v6 handler when
    /// running with Scoring:Engine = "mono".
    /// </summary>
    Task UpsertMonoBatchAsync(
        string cvHash,
        string scoringVersion,
        IReadOnlyList<MonoCacheUpsert> entries,
        CancellationToken ct = default);
}


public sealed record JudgeCacheUpsert(
    Guid VacancyId,
    double JudgeScore,
    Domain.Scoring.Verdict JudgeVerdict);


public sealed record ReasonCacheUpsert(
    Guid VacancyId,
    string StrengthsEn,
    string StrengthsUk,
    string GapsEn,
    string GapsUk,
    string RecommendationEn,
    string RecommendationUk);


public sealed record MonoCacheUpsert(
    Guid VacancyId,
    string MonoResultJson);
