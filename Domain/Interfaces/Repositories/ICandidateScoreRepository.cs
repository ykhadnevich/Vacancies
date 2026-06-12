using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface ICandidateScoreRepository
{
    /// <summary>
    /// All current scores for a (list × vacancy) pair. The handler joins by
    /// <see cref="CandidateScore.RecruiterCandidateId"/> against the list membership
    /// to filter to candidates that are still in the list at read time.
    /// </summary>
    Task<IReadOnlyList<CandidateScore>> GetForVacancyAndListAsync(
        Guid vacancyId,
        Guid candidateListId,
        CancellationToken ct = default);

    /// <summary>Returns the subset of candidate IDs that already have a score for the given vacancy.</summary>
    Task<IReadOnlySet<Guid>> GetScoredCandidateIdsAsync(
        Guid vacancyId,
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken ct = default);

    Task<CandidateScore?> GetAsync(
        Guid vacancyId,
        Guid recruiterCandidateId,
        CancellationToken ct = default);

    /// <summary>
    /// Insert new or update existing row keyed by (VacancyId, RecruiterCandidateId).
    /// </summary>
    Task UpsertAsync(CandidateScore score, CancellationToken ct = default);

    Task UpsertBatchAsync(IReadOnlyList<CandidateScore> scores, CancellationToken ct = default);

    /// <summary>Total number of candidates scored against the given vacancy.</summary>
    Task<int> CountForVacancyAsync(Guid vacancyId, CancellationToken ct = default);
}
