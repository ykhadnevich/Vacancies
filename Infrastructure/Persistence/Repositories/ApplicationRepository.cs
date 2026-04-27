using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;

namespace Infrastructure.Persistence.Repositories;

public class ApplicationRepository : IApplicationRepository
{
    private readonly AppDbContext _context;

    public ApplicationRepository(AppDbContext context) => _context = context;

    public async Task<ApplicationTracker?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
        => await _context.Applications.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<ApplicationTracker>> GetByUserIdAsync(
        Guid userId, CancellationToken ct = default)
        => await _context.Applications
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.AddedAt)
            .ToListAsync(ct);

    public async Task AddAsync(
        ApplicationTracker application, CancellationToken ct = default)
    {
        await _context.Applications.AddAsync(application, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(
        ApplicationTracker application, CancellationToken ct = default)
    {
        _context.Applications.Update(application);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var application = await _context.Applications.FindAsync(new object[] { id }, ct);
        if (application is not null)
        {
            _context.Applications.Remove(application);
            await _context.SaveChangesAsync(ct);
        }
    }
}