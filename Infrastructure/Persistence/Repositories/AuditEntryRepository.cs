using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Interfaces.Repositories;

namespace Infrastructure.Persistence.Repositories;

public sealed class AuditEntryRepository : IAuditEntryRepository
{
    private readonly AppDbContext _context;

    public AuditEntryRepository(AppDbContext context) => _context = context;

    public async Task AddAsync(AuditEntry entry, CancellationToken ct = default)
    {
        await _context.AuditEntries.AddAsync(entry, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<AuditEntry>> QueryByUserAsync(
        Guid userId, int limit, CancellationToken ct = default)
        => await _context.AuditEntries
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.Timestamp)
            .Take(limit)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<AuditEntry>> QueryByEntityAsync(
        string entityType, Guid entityId, CancellationToken ct = default)
        => await _context.AuditEntries
            .AsNoTracking()
            .Where(e => e.EntityType == entityType && e.EntityId == entityId)
            .OrderByDescending(e => e.Timestamp)
            .ToListAsync(ct);
}
