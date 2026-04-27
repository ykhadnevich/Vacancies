

using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface ISavedUrlRepository
{
    Task<SavedUrl?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<SavedUrl?> GetByUrlAsync(string url, CancellationToken ct = default);
    Task<IReadOnlyList<SavedUrl>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(SavedUrl savedUrl, CancellationToken ct = default);
    Task UpdateAsync(SavedUrl savedUrl, CancellationToken ct = default);
}