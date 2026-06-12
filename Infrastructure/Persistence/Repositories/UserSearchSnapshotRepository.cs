using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories;

public sealed class UserSearchSnapshotRepository : IUserSearchSnapshotRepository
{
    private readonly AppDbContext _context;

    public UserSearchSnapshotRepository(AppDbContext context) => _context = context;

    public async Task<UserSearchSnapshot?> GetByQueryAsync(
        Guid userId, string queryHash, CancellationToken ct = default)
    {
        if (userId == Guid.Empty || string.IsNullOrWhiteSpace(queryHash))
            return null;

        return await _context.UserSearchSnapshots
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == userId && s.QueryHash == queryHash, ct);
    }

    /// <summary>
    /// Raw-SQL upsert keyed by (UserId, QueryHash) — mirrors the
    /// ScoringCacheRepository pattern so jsonb roundtrips cleanly.
    /// </summary>
    public async Task UpsertAsync(UserSearchSnapshot snapshot, CancellationToken ct = default)
    {
        const string sql =
            "INSERT INTO \"UserSearchSnapshots\" " +
            "(\"Id\", \"UserId\", \"QueryHash\", \"Keywords\", \"ResponseJson\", \"ExecutedAt\") " +
            "VALUES (@id, @userId, @queryHash, @keywords, @json, @ts) " +
            "ON CONFLICT (\"UserId\", \"QueryHash\") DO UPDATE SET " +
            "\"ResponseJson\" = EXCLUDED.\"ResponseJson\", " +
            "\"ExecutedAt\"   = EXCLUDED.\"ExecutedAt\", " +
            "\"Keywords\"     = EXCLUDED.\"Keywords\";";

        var parameters = new object[]
        {
            new NpgsqlParameter("id",        NpgsqlDbType.Uuid)        { Value = snapshot.Id },
            new NpgsqlParameter("userId",    NpgsqlDbType.Uuid)        { Value = snapshot.UserId },
            new NpgsqlParameter("queryHash", NpgsqlDbType.Varchar)     { Value = snapshot.QueryHash },
            new NpgsqlParameter("keywords",  NpgsqlDbType.Varchar)     { Value = snapshot.Keywords },
            new NpgsqlParameter("json",      NpgsqlDbType.Jsonb)       { Value = snapshot.ResponseJson },
            new NpgsqlParameter("ts",        NpgsqlDbType.TimestampTz) { Value = snapshot.ExecutedAt },
        };

        await _context.Database.ExecuteSqlRawAsync(sql, parameters, ct);
    }

    public async Task DeleteOlderThanAsync(DateTime threshold, CancellationToken ct = default)
    {
        await _context.UserSearchSnapshots
            .Where(s => s.ExecutedAt < threshold)
            .ExecuteDeleteAsync(ct);
    }
}
