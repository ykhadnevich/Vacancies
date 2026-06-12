using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class CandidateListRepository : ICandidateListRepository
{
    private readonly AppDbContext _context;

    public CandidateListRepository(AppDbContext context) => _context = context;

    public async Task<CandidateList?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.CandidateLists.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<CandidateList>> ListByRecruiterAsync(
        Guid recruiterUserId, CancellationToken ct = default)
        => await _context.CandidateLists
            .AsNoTracking()
            .Where(l => l.RecruiterUserId == recruiterUserId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync(ct);

    public async Task AddAsync(CandidateList list, CancellationToken ct = default)
    {
        await _context.CandidateLists.AddAsync(list, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(CandidateList list, CancellationToken ct = default)
    {
        _context.CandidateLists.Update(list);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var list = await _context.CandidateLists.FindAsync(new object[] { id }, ct);
        if (list is null) return;

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Drop list-only links; candidates themselves stay (they can live in other lists).
            await _context.CandidateListMemberships
                .Where(m => m.CandidateListId == id)
                .ExecuteDeleteAsync(ct);

            _context.CandidateLists.Remove(list);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<int> CountCandidatesAsync(Guid listId, CancellationToken ct = default)
        => await _context.CandidateListMemberships
            .CountAsync(m => m.CandidateListId == listId, ct);
}
