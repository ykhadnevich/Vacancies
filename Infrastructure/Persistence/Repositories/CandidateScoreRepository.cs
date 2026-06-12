using System.Text;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories;

public sealed class CandidateScoreRepository : ICandidateScoreRepository
{
    private readonly AppDbContext _context;

    public CandidateScoreRepository(AppDbContext context) => _context = context;

    /// <summary>
    /// Joins through CandidateListMemberships so candidates removed from the list
    /// don't surface in the ranking even if a stale CandidateScore row still exists.
    /// </summary>
    public async Task<IReadOnlyList<CandidateScore>> GetForVacancyAndListAsync(
        Guid vacancyId,
        Guid candidateListId,
        CancellationToken ct = default)
    {
        return await _context.CandidateScores
            .AsNoTracking()
            .Where(s => s.VacancyId == vacancyId)
            .Join(_context.CandidateListMemberships
                      .Where(m => m.CandidateListId == candidateListId),
                  s => s.RecruiterCandidateId,
                  m => m.RecruiterCandidateId,
                  (s, m) => s)
            .OrderByDescending(s => s.Score)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlySet<Guid>> GetScoredCandidateIdsAsync(
        Guid vacancyId,
        IReadOnlyCollection<Guid> candidateIds,
        CancellationToken ct = default)
    {
        if (candidateIds.Count == 0) return new HashSet<Guid>();
        var ids = candidateIds.Distinct().ToList();
        var scored = await _context.CandidateScores
            .AsNoTracking()
            .Where(s => s.VacancyId == vacancyId && ids.Contains(s.RecruiterCandidateId))
            .Select(s => s.RecruiterCandidateId)
            .ToListAsync(ct);
        return new HashSet<Guid>(scored);
    }

    public async Task<CandidateScore?> GetAsync(
        Guid vacancyId,
        Guid recruiterCandidateId,
        CancellationToken ct = default)
        => await _context.CandidateScores
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.VacancyId == vacancyId
                                    && s.RecruiterCandidateId == recruiterCandidateId, ct);

    public async Task UpsertAsync(CandidateScore score, CancellationToken ct = default)
    {
        await UpsertBatchAsync(new[] { score }, ct);
    }

    /// <summary>
    /// Raw SQL upsert keyed by (VacancyId, RecruiterCandidateId) — mirrors the
    /// ScoringCacheRepository.UpsertMonoBatchAsync pattern. ON CONFLICT updates
    /// the score payload but preserves the original Id.
    /// </summary>
    public async Task UpsertBatchAsync(
        IReadOnlyList<CandidateScore> scores,
        CancellationToken ct = default)
    {
        if (scores.Count == 0) return;

        var sb = new StringBuilder(
            "INSERT INTO \"CandidateScores\" " +
            "(\"Id\", \"VacancyId\", \"RecruiterCandidateId\", \"Score\", " +
            " \"ScoringVersion\", \"ScoringResultJson\", \"ScoredAt\") VALUES ");

        var parameters = new List<NpgsqlParameter>(scores.Count * 7);

        for (int i = 0; i < scores.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(')
              .Append($"@p{i}_id, @p{i}_v, @p{i}_c, @p{i}_s, @p{i}_sv, @p{i}_json, @p{i}_ts")
              .Append(')');

            var s = scores[i];
            parameters.Add(new NpgsqlParameter($"p{i}_id",   NpgsqlDbType.Uuid)        { Value = s.Id });
            parameters.Add(new NpgsqlParameter($"p{i}_v",    NpgsqlDbType.Uuid)        { Value = s.VacancyId });
            parameters.Add(new NpgsqlParameter($"p{i}_c",    NpgsqlDbType.Uuid)        { Value = s.RecruiterCandidateId });
            parameters.Add(new NpgsqlParameter($"p{i}_s",    NpgsqlDbType.Double)      { Value = s.Score });
            parameters.Add(new NpgsqlParameter($"p{i}_sv",   NpgsqlDbType.Varchar)     { Value = s.ScoringVersion });
            parameters.Add(new NpgsqlParameter($"p{i}_json", NpgsqlDbType.Jsonb)       { Value = s.ScoringResultJson });
            parameters.Add(new NpgsqlParameter($"p{i}_ts",   NpgsqlDbType.TimestampTz) { Value = s.ScoredAt });
        }

        // Conflict on the unique index (VacancyId, RecruiterCandidateId) — re-analyse refreshes the row.
        sb.Append(
            " ON CONFLICT (\"VacancyId\", \"RecruiterCandidateId\") DO UPDATE SET " +
            "\"Score\" = EXCLUDED.\"Score\", " +
            "\"ScoringVersion\" = EXCLUDED.\"ScoringVersion\", " +
            "\"ScoringResultJson\" = EXCLUDED.\"ScoringResultJson\", " +
            "\"ScoredAt\" = EXCLUDED.\"ScoredAt\";");

        await _context.Database.ExecuteSqlRawAsync(sb.ToString(), (IEnumerable<object>)parameters, ct);
    }

    public async Task<int> CountForVacancyAsync(Guid vacancyId, CancellationToken ct = default)
        => await _context.CandidateScores
            .CountAsync(s => s.VacancyId == vacancyId, ct);
}
