using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Infrastructure.Persistence.Repositories;

public class SavedUrlRepository : ISavedUrlRepository
{
    private readonly AppDbContext _context;

    public SavedUrlRepository(AppDbContext context)
    {
        _context = context;
    }

    public async Task<SavedUrl?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.SavedUrls.FindAsync(new object[] { id }, ct);

    public async Task<SavedUrl?> GetByUrlAsync(string url, CancellationToken ct = default)
        => await _context.SavedUrls.FirstOrDefaultAsync(s => s.Url == url, ct);

    public async Task<IReadOnlyList<SavedUrl>> GetAllAsync(CancellationToken ct = default)
        => await _context.SavedUrls.OrderByDescending(s => s.CreatedAt).ToListAsync(ct);

    public async Task AddAsync(SavedUrl savedUrl, CancellationToken ct = default)
    {
        await _context.SavedUrls.AddAsync(savedUrl, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(SavedUrl savedUrl, CancellationToken ct = default)
    {
        _context.SavedUrls.Update(savedUrl);
        await _context.SaveChangesAsync(ct);
    }
}
