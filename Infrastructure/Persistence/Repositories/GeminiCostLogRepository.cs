using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Interfaces.Repositories;

namespace Infrastructure.Persistence.Repositories;

public sealed class GeminiCostLogRepository : IGeminiCostLogRepository
{
    private readonly AppDbContext _context;

    public GeminiCostLogRepository(AppDbContext context) => _context = context;

    public async Task AddRangeAsync(
        IEnumerable<GeminiCostLogEntry> entries, CancellationToken ct = default)
    {
        await _context.GeminiCostLog.AddRangeAsync(entries, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<GeminiCostLogEntry>> QueryAsync(
        DateTime from, DateTime to, CancellationToken ct = default)
        => await _context.GeminiCostLog
            .AsNoTracking()
            .Where(e => e.Timestamp >= from && e.Timestamp < to)
            .OrderBy(e => e.Timestamp)
            .ToListAsync(ct);
}
