using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IAuditEntryRepository
{
    Task AddAsync(AuditEntry entry, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> QueryByUserAsync(
        Guid userId, int limit, CancellationToken ct = default);

    Task<IReadOnlyList<AuditEntry>> QueryByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default);
}
