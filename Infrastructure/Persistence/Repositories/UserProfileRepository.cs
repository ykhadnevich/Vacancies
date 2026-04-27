using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;

namespace Infrastructure.Persistence.Repositories;

public class UserProfileRepository : IUserProfileRepository
{
    private readonly AppDbContext _context;

    public UserProfileRepository(AppDbContext context) => _context = context;

    public async Task<UserProfile?> GetByIdAsync(
        Guid id, CancellationToken ct = default)
        => await _context.UserProfiles.FindAsync(new object[] { id }, ct);

    public async Task<UserProfile?> GetByEmailAsync(
        string email, CancellationToken ct = default)
        => await _context.UserProfiles
            .FirstOrDefaultAsync(u => u.Email == email, ct);

    public async Task AddAsync(UserProfile profile, CancellationToken ct = default)
    {
        await _context.UserProfiles.AddAsync(profile, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(UserProfile profile, CancellationToken ct = default)
    {
        _context.UserProfiles.Update(profile);
        await _context.SaveChangesAsync(ct);
    }
}