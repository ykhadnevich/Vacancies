using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IUserProfileRepository
{
    Task<UserProfile?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<UserProfile?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task AddAsync(UserProfile profile, CancellationToken ct = default);
    Task UpdateAsync(UserProfile profile, CancellationToken ct = default);


    Task<IReadOnlyList<UserProfile>> GetUsersWithCvAsync(CancellationToken ct = default);


    Task<IReadOnlyList<UserProfile>> GetUsersWithoutCvSummaryAsync(CancellationToken ct = default);


    Task<IReadOnlyList<UserProfile>> GetUsersNeedingNormalizationAsync(
        string currentExpectedModelVersionPrefix,
        CancellationToken ct = default);


    Task<bool> DeleteUserCascadeAsync(Guid userId, CancellationToken ct = default);
}