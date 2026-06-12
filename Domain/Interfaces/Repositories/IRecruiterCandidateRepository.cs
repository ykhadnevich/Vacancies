using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IRecruiterCandidateRepository
{
    Task<RecruiterCandidate?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<RecruiterCandidate>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids,
        CancellationToken ct = default);

    Task AddAsync(RecruiterCandidate candidate, CancellationToken ct = default);
    Task UpdateAsync(RecruiterCandidate candidate, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// Adds an existing candidate to a list. No-op if the membership row already exists
    /// (composite PK on the database guarantees idempotency).
    /// </summary>
    Task AddToListAsync(Guid listId, Guid candidateId, CancellationToken ct = default);
    Task RemoveFromListAsync(Guid listId, Guid candidateId, CancellationToken ct = default);

    /// <summary>
    /// All candidates currently in the given list, regardless of normalization status.
    /// Caller filters by Status when running analysis.
    /// </summary>
    Task<IReadOnlyList<RecruiterCandidate>> ListByListAsync(Guid listId, CancellationToken ct = default);

    /// <summary>All candidates owned by the recruiter (across all lists).</summary>
    Task<IReadOnlyList<RecruiterCandidate>> ListByRecruiterAsync(
        Guid recruiterUserId,
        CancellationToken ct = default);
}
