using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IApplicationRepository
{
    Task<ApplicationTracker?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<ApplicationTracker>> GetByUserIdAsync(Guid userId, CancellationToken ct = default);
    Task AddAsync(ApplicationTracker application, CancellationToken ct = default);
    Task UpdateAsync(ApplicationTracker application, CancellationToken ct = default);
    Task DeleteAsync(Guid id, CancellationToken ct = default);
}