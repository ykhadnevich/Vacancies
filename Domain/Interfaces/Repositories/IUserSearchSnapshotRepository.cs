using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IUserSearchSnapshotRepository
{
    Task<UserSearchSnapshot?> GetByQueryAsync(
        Guid userId,
        string queryHash,
        CancellationToken ct = default);

    /// <summary>
    /// Upserts on the natural key <c>(UserId, QueryHash)</c>. Replaces the
    /// response payload and bumps <c>ExecutedAt</c> when a row already exists.
    /// </summary>
    Task UpsertAsync(UserSearchSnapshot snapshot, CancellationToken ct = default);

    Task DeleteOlderThanAsync(DateTime threshold, CancellationToken ct = default);
}
