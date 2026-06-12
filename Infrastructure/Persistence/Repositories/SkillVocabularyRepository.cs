using System.Text;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace Infrastructure.Persistence.Repositories;


public sealed class SkillVocabularyRepository : ISkillVocabularyRepository
{
    private readonly AppDbContext _context;

    public SkillVocabularyRepository(AppDbContext context) => _context = context;

    public async Task<IReadOnlyDictionary<string, SkillVocabularyEntry>> GetByCanonicalLowerAsync(
        IReadOnlyCollection<string> canonicalLowers,
        CancellationToken ct = default)
    {
        if (canonicalLowers.Count == 0)
            return new Dictionary<string, SkillVocabularyEntry>(StringComparer.OrdinalIgnoreCase);


        var keys = canonicalLowers
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim().ToLowerInvariant())
            .Distinct()
            .ToList();

        if (keys.Count == 0)
            return new Dictionary<string, SkillVocabularyEntry>(StringComparer.OrdinalIgnoreCase);

        var rows = await _context.Set<SkillVocabularyEntry>()
            .AsNoTracking()
            .Where(e => keys.Contains(e.CanonicalLower))
            .ToListAsync(ct);

        return rows.ToDictionary(
            e => e.CanonicalLower,
            e => e,
            StringComparer.OrdinalIgnoreCase);
    }

    public async Task UpsertBatchAsync(
        IReadOnlyCollection<SkillVocabularyEntry> entries,
        CancellationToken ct = default)
    {
        if (entries.Count == 0) return;


        var sb = new StringBuilder(
            "INSERT INTO \"SkillVocabulary\" " +
            "(\"CanonicalLower\", \"Canonical\", \"SynonymsJson\", \"Domain\", " +
            " \"Confidence\", \"Source\", \"ModelVersion\", \"CreatedAt\", \"UpdatedAt\") " +
            "VALUES ");

        var parameters = new List<NpgsqlParameter>(entries.Count * 9);
        var i = 0;
        foreach (var e in entries)
        {
            if (i > 0) sb.Append(',');
            sb.Append('(')
              .Append($"@p{i}_cl, @p{i}_c, @p{i}_sj, @p{i}_d, @p{i}_cf, @p{i}_s, @p{i}_mv, @p{i}_ca, @p{i}_ua")
              .Append(')');

            parameters.Add(new NpgsqlParameter($"p{i}_cl", NpgsqlDbType.Varchar) { Value = e.CanonicalLower });
            parameters.Add(new NpgsqlParameter($"p{i}_c",  NpgsqlDbType.Varchar) { Value = e.Canonical });
            parameters.Add(new NpgsqlParameter($"p{i}_sj", NpgsqlDbType.Text)    { Value = e.SynonymsJson });
            parameters.Add(new NpgsqlParameter($"p{i}_d",  NpgsqlDbType.Varchar) { Value = e.Domain });
            parameters.Add(new NpgsqlParameter($"p{i}_cf", NpgsqlDbType.Numeric) { Value = e.Confidence });
            parameters.Add(new NpgsqlParameter($"p{i}_s",  NpgsqlDbType.Varchar) { Value = e.Source });
            parameters.Add(new NpgsqlParameter($"p{i}_mv", NpgsqlDbType.Varchar)
            {
                Value = (object?)e.ModelVersion ?? DBNull.Value
            });
            parameters.Add(new NpgsqlParameter($"p{i}_ca", NpgsqlDbType.TimestampTz) { Value = e.CreatedAt });
            parameters.Add(new NpgsqlParameter($"p{i}_ua", NpgsqlDbType.TimestampTz) { Value = e.UpdatedAt });
            i++;
        }

        sb.Append(" ON CONFLICT (\"CanonicalLower\") DO NOTHING;");

        await _context.Database.ExecuteSqlRawAsync(sb.ToString(), parameters, ct);
    }
}
