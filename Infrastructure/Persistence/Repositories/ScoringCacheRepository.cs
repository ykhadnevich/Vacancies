using System.Text;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories;


public sealed class ScoringCacheRepository : IScoringCacheRepository
{
    private readonly AppDbContext _context;

    public ScoringCacheRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<Guid, ScoringCacheEntry>> GetForCvAsync(
        string cvHash,
        IReadOnlyCollection<Guid> vacancyIds,
        string scoringVersion,
        CancellationToken ct = default)
    {
        if (vacancyIds.Count == 0)
            return new Dictionary<Guid, ScoringCacheEntry>();
        if (string.IsNullOrWhiteSpace(cvHash) || string.IsNullOrWhiteSpace(scoringVersion))
            return new Dictionary<Guid, ScoringCacheEntry>();


        var ids = vacancyIds.Distinct().ToList();
        var rows = await _context.Set<ScoringCacheEntry>()
            .AsNoTracking()
            .Where(e => e.CvHash == cvHash
                     && e.ScoringVersion == scoringVersion
                     && ids.Contains(e.VacancyId))
            .ToListAsync(ct);

        return rows.ToDictionary(e => e.VacancyId);
    }

    public async Task UpsertJudgeBatchAsync(
        string cvHash,
        string scoringVersion,
        IReadOnlyList<JudgeCacheUpsert> entries,
        CancellationToken ct = default)
    {
        if (entries.Count == 0) return;
        if (string.IsNullOrWhiteSpace(cvHash) || string.IsNullOrWhiteSpace(scoringVersion))
            return;


        var sb = new StringBuilder(
            "INSERT INTO \"ScoringCache\" " +
            "(\"CvHash\", \"VacancyId\", \"ScoringVersion\", " +
            " \"JudgeScore\", \"JudgeVerdict\", " +
            " \"CreatedAt\", \"UpdatedAt\") VALUES ");

        var parameters = new List<NpgsqlParameter>(entries.Count * 5 + 2)
        {
            new NpgsqlParameter("cvHash", NpgsqlDbType.Varchar) { Value = cvHash },
            new NpgsqlParameter("sv",     NpgsqlDbType.Varchar) { Value = scoringVersion },
        };

        var now = DateTime.UtcNow;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(')
              .Append($"@cvHash, @p{i}_v, @sv, @p{i}_js, @p{i}_jv, @p{i}_ts, @p{i}_ts")
              .Append(')');

            parameters.Add(new NpgsqlParameter($"p{i}_v",  NpgsqlDbType.Uuid)    { Value = entries[i].VacancyId });
            parameters.Add(new NpgsqlParameter($"p{i}_js", NpgsqlDbType.Double)  { Value = entries[i].JudgeScore });
            parameters.Add(new NpgsqlParameter($"p{i}_jv", NpgsqlDbType.Integer) { Value = (int)entries[i].JudgeVerdict });
            parameters.Add(new NpgsqlParameter($"p{i}_ts", NpgsqlDbType.TimestampTz) { Value = now });
        }

        sb.Append(
            " ON CONFLICT (\"CvHash\", \"VacancyId\", \"ScoringVersion\") DO UPDATE SET " +
            "\"JudgeScore\" = EXCLUDED.\"JudgeScore\", " +
            "\"JudgeVerdict\" = EXCLUDED.\"JudgeVerdict\", " +
            "\"UpdatedAt\" = EXCLUDED.\"UpdatedAt\";");


        await _context.Database.ExecuteSqlRawAsync(sb.ToString(), (IEnumerable<object>)parameters, ct);
    }

    public async Task UpsertReasonBatchAsync(
        string cvHash,
        string scoringVersion,
        IReadOnlyList<ReasonCacheUpsert> entries,
        CancellationToken ct = default)
    {
        if (entries.Count == 0) return;
        if (string.IsNullOrWhiteSpace(cvHash) || string.IsNullOrWhiteSpace(scoringVersion))
            return;


        entries = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.StrengthsEn)
                     && !string.IsNullOrWhiteSpace(e.StrengthsUk)
                     && !string.IsNullOrWhiteSpace(e.GapsEn)
                     && !string.IsNullOrWhiteSpace(e.GapsUk)
                     && !string.IsNullOrWhiteSpace(e.RecommendationEn)
                     && !string.IsNullOrWhiteSpace(e.RecommendationUk))
            .ToList();
        if (entries.Count == 0) return;


        var sb = new StringBuilder(
            "INSERT INTO \"ScoringCache\" " +
            "(\"CvHash\", \"VacancyId\", \"ScoringVersion\", " +
            " \"StrengthsEn\", \"StrengthsUk\", \"GapsEn\", \"GapsUk\", " +
            " \"RecommendationEn\", \"RecommendationUk\", " +
            " \"CreatedAt\", \"UpdatedAt\") VALUES ");

        var parameters = new List<NpgsqlParameter>(entries.Count * 9 + 2)
        {
            new NpgsqlParameter("cvHash", NpgsqlDbType.Varchar) { Value = cvHash },
            new NpgsqlParameter("sv",     NpgsqlDbType.Varchar) { Value = scoringVersion },
        };

        var now = DateTime.UtcNow;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(')
              .Append($"@cvHash, @p{i}_v, @sv, ")
              .Append($"@p{i}_se, @p{i}_su, @p{i}_ge, @p{i}_gu, @p{i}_re, @p{i}_ru, ")
              .Append($"@p{i}_ts, @p{i}_ts")
              .Append(')');

            var e = entries[i];
            parameters.Add(new NpgsqlParameter($"p{i}_v",  NpgsqlDbType.Uuid) { Value = e.VacancyId });
            parameters.Add(new NpgsqlParameter($"p{i}_se", NpgsqlDbType.Text) { Value = e.StrengthsEn });
            parameters.Add(new NpgsqlParameter($"p{i}_su", NpgsqlDbType.Text) { Value = e.StrengthsUk });
            parameters.Add(new NpgsqlParameter($"p{i}_ge", NpgsqlDbType.Text) { Value = e.GapsEn });
            parameters.Add(new NpgsqlParameter($"p{i}_gu", NpgsqlDbType.Text) { Value = e.GapsUk });
            parameters.Add(new NpgsqlParameter($"p{i}_re", NpgsqlDbType.Text) { Value = e.RecommendationEn });
            parameters.Add(new NpgsqlParameter($"p{i}_ru", NpgsqlDbType.Text) { Value = e.RecommendationUk });
            parameters.Add(new NpgsqlParameter($"p{i}_ts", NpgsqlDbType.TimestampTz) { Value = now });
        }

        sb.Append(
            " ON CONFLICT (\"CvHash\", \"VacancyId\", \"ScoringVersion\") DO UPDATE SET " +
            "\"StrengthsEn\" = EXCLUDED.\"StrengthsEn\", " +
            "\"StrengthsUk\" = EXCLUDED.\"StrengthsUk\", " +
            "\"GapsEn\" = EXCLUDED.\"GapsEn\", " +
            "\"GapsUk\" = EXCLUDED.\"GapsUk\", " +
            "\"RecommendationEn\" = EXCLUDED.\"RecommendationEn\", " +
            "\"RecommendationUk\" = EXCLUDED.\"RecommendationUk\", " +
            "\"UpdatedAt\" = EXCLUDED.\"UpdatedAt\";");


        await _context.Database.ExecuteSqlRawAsync(sb.ToString(), (IEnumerable<object>)parameters, ct);
    }


    public async Task UpsertMonoBatchAsync(
        string cvHash,
        string scoringVersion,
        IReadOnlyList<MonoCacheUpsert> entries,
        CancellationToken ct = default)
    {
        if (entries.Count == 0) return;
        if (string.IsNullOrWhiteSpace(cvHash) || string.IsNullOrWhiteSpace(scoringVersion))
            return;

        entries = entries
            .Where(e => !string.IsNullOrWhiteSpace(e.MonoResultJson))
            .ToList();
        if (entries.Count == 0) return;


        var sb = new StringBuilder(
            "INSERT INTO \"ScoringCache\" " +
            "(\"CvHash\", \"VacancyId\", \"ScoringVersion\", " +
            " \"MonoResultJson\", " +
            " \"CreatedAt\", \"UpdatedAt\") VALUES ");

        var parameters = new List<NpgsqlParameter>(entries.Count * 3 + 2)
        {
            new NpgsqlParameter("cvHash", NpgsqlDbType.Varchar) { Value = cvHash },
            new NpgsqlParameter("sv",     NpgsqlDbType.Varchar) { Value = scoringVersion },
        };

        var now = DateTime.UtcNow;
        for (int i = 0; i < entries.Count; i++)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(')
              .Append($"@cvHash, @p{i}_v, @sv, @p{i}_json, @p{i}_ts, @p{i}_ts")
              .Append(')');

            parameters.Add(new NpgsqlParameter($"p{i}_v",    NpgsqlDbType.Uuid)        { Value = entries[i].VacancyId });
            parameters.Add(new NpgsqlParameter($"p{i}_json", NpgsqlDbType.Jsonb)       { Value = entries[i].MonoResultJson });
            parameters.Add(new NpgsqlParameter($"p{i}_ts",   NpgsqlDbType.TimestampTz) { Value = now });
        }

        sb.Append(
            " ON CONFLICT (\"CvHash\", \"VacancyId\", \"ScoringVersion\") DO UPDATE SET " +
            "\"MonoResultJson\" = EXCLUDED.\"MonoResultJson\", " +
            "\"UpdatedAt\" = EXCLUDED.\"UpdatedAt\";");


        await _context.Database.ExecuteSqlRawAsync(sb.ToString(), (IEnumerable<object>)parameters, ct);
    }
}
