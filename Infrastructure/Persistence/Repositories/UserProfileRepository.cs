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


    public async Task<IReadOnlyList<UserProfile>> GetUsersWithCvAsync(
        CancellationToken ct = default)
        => await _context.UserProfiles
            .Where(u => u.CvRawText != null && u.CvRawText != string.Empty)
            .ToListAsync(ct);


    public async Task<IReadOnlyList<UserProfile>> GetUsersWithoutCvSummaryAsync(
        CancellationToken ct = default)
        => await _context.UserProfiles
            .Where(u => u.CvRawText != null && u.CvRawText != string.Empty
                     && (u.CvSummary == null || u.CvSummary == string.Empty))
            .ToListAsync(ct);


    public async Task<IReadOnlyList<UserProfile>> GetUsersNeedingNormalizationAsync(
        string currentExpectedModelVersionPrefix,
        CancellationToken ct = default)
        => await _context.UserProfiles
            .Where(u => u.CvRawText != null && u.CvRawText != string.Empty
                     && (u.CvSummary == null
                         || u.CvSummary == string.Empty
                         || u.CvSummaryModelVersion == null
                         || !u.CvSummaryModelVersion.StartsWith(currentExpectedModelVersionPrefix)))
            .ToListAsync(ct);


    public async Task<bool> DeleteUserCascadeAsync(
        Guid userId, CancellationToken ct = default)
    {
        var profile = await _context.UserProfiles
            .FindAsync(new object[] { userId }, ct);
        if (profile is null) return false;

        var cvVersionId = profile.CvVersionId;


        await using var tx = await _context.Database.BeginTransactionAsync(ct);
        try
        {

            await _context.Applications
                .Where(a => a.UserId == userId)
                .ExecuteDeleteAsync(ct);


            await _context.RelevanceExplanations
                .Where(r => r.CvVersionId == cvVersionId)
                .ExecuteDeleteAsync(ct);


            _context.UserProfiles.Remove(profile);
            await _context.SaveChangesAsync(ct);

            await tx.CommitAsync(ct);
            return true;
        }
        catch
        {
            await tx.RollbackAsync(ct);
            throw;
        }
    }
}
