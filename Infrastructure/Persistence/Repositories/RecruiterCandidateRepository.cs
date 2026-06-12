using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public sealed class RecruiterCandidateRepository : IRecruiterCandidateRepository
{
    private readonly AppDbContext _context;

    public RecruiterCandidateRepository(AppDbContext context) => _context = context;

    public async Task<RecruiterCandidate?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.RecruiterCandidates.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<RecruiterCandidate>> GetByIdsAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken ct = default)
    {
        if (ids.Count == 0) return Array.Empty<RecruiterCandidate>();
        var idList = ids.Distinct().ToList();
        return await _context.RecruiterCandidates
            .Where(c => idList.Contains(c.Id))
            .ToListAsync(ct);
    }

    public async Task AddAsync(RecruiterCandidate candidate, CancellationToken ct = default)
    {
        await _context.RecruiterCandidates.AddAsync(candidate, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RecruiterCandidate candidate, CancellationToken ct = default)
    {
        _context.RecruiterCandidates.Update(candidate);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var candidate = await _context.RecruiterCandidates.FindAsync(new object[] { id }, ct);
        if (candidate is null) return;

        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {
            // Drop all list memberships and scores for this candidate.
            await _context.CandidateListMemberships
                .Where(m => m.RecruiterCandidateId == id)
                .ExecuteDeleteAsync(ct);

            await _context.CandidateScores
                .Where(s => s.RecruiterCandidateId == id)
                .ExecuteDeleteAsync(ct);

            _context.RecruiterCandidates.Remove(candidate);
            await _context.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }

    public async Task AddToListAsync(Guid listId, Guid candidateId, CancellationToken ct = default)
    {
        var exists = await _context.CandidateListMemberships
            .AnyAsync(m => m.CandidateListId == listId && m.RecruiterCandidateId == candidateId, ct);
        if (exists) return;

        var membership = CandidateListMembership.Create(listId, candidateId);
        await _context.CandidateListMemberships.AddAsync(membership, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task RemoveFromListAsync(Guid listId, Guid candidateId, CancellationToken ct = default)
    {
        await _context.CandidateListMemberships
            .Where(m => m.CandidateListId == listId && m.RecruiterCandidateId == candidateId)
            .ExecuteDeleteAsync(ct);
    }

    public async Task<IReadOnlyList<RecruiterCandidate>> ListByListAsync(
        Guid listId, CancellationToken ct = default)
    {
        return await _context.CandidateListMemberships
            .Where(m => m.CandidateListId == listId)
            .Join(_context.RecruiterCandidates,
                  m => m.RecruiterCandidateId,
                  c => c.Id,
                  (m, c) => new { Membership = m, Candidate = c })
            .OrderByDescending(x => x.Membership.AddedAt)
            .Select(x => x.Candidate)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<RecruiterCandidate>> ListByRecruiterAsync(
        Guid recruiterUserId, CancellationToken ct = default)
        => await _context.RecruiterCandidates
            .AsNoTracking()
            .Where(c => c.RecruiterUserId == recruiterUserId)
            .OrderByDescending(c => c.AddedAt)
            .ToListAsync(ct);
}
