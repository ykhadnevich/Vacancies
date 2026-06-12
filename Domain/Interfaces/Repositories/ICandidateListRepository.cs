using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface ICandidateListRepository
{
    Task<CandidateList?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<CandidateList>> ListByRecruiterAsync(Guid recruiterUserId, CancellationToken ct = default);
    Task AddAsync(CandidateList list, CancellationToken ct = default);
    Task UpdateAsync(CandidateList list, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);

    /// <summary>Count of candidates linked to a list — for cabinet summary screens.</summary>
    Task<int> CountCandidatesAsync(Guid listId, CancellationToken ct = default);
}
