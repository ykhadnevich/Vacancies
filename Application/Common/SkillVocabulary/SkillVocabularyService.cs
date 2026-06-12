using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Application.Common.SkillVocabulary;


public sealed class SkillVocabularyService : ISkillVocabularyService
{
    private readonly ISkillVocabularyRepository _repo;
    private readonly IBatchSkillExpander _expander;
    private readonly ILogger<SkillVocabularyService> _logger;

    public string Version => "global_vocab_v1+" + _expander.Version;

    public SkillVocabularyService(
        ISkillVocabularyRepository repo,
        IBatchSkillExpander expander,
        ILogger<SkillVocabularyService> logger)
    {
        _repo = repo;
        _expander = expander;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> ResolveSynonymsAsync(
        IReadOnlyCollection<string> skills,
        string? roleFamilyHint,
        CancellationToken ct = default)
    {

        var canonicalToOriginal = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in skills)
        {
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var trimmed = raw.Trim();
            var lower = trimmed.ToLowerInvariant();
            if (!canonicalToOriginal.ContainsKey(lower))
                canonicalToOriginal[lower] = trimmed;
        }

        if (canonicalToOriginal.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        var existing = await _repo.GetByCanonicalLowerAsync(canonicalToOriginal.Keys.ToList(), ct);


        var unknownLowers = canonicalToOriginal.Keys
            .Where(k => !existing.ContainsKey(k))
            .ToList();

        var domain = string.IsNullOrWhiteSpace(roleFamilyHint) ? "general" : roleFamilyHint;
        Dictionary<string, string>? batchResult = null;

        if (unknownLowers.Count > 0)
        {
            var unknownOriginals = unknownLowers.Select(l => canonicalToOriginal[l]).ToList();
            _logger.LogInformation(
                "Skill vocab: total={Total} hits={Hits} unknown={Unknown} — calling batch LLM",
                canonicalToOriginal.Count, existing.Count, unknownLowers.Count);

            try
            {
                var llm = await _expander.ExpandBatchAsync(unknownOriginals, roleFamilyHint, ct);
                batchResult = new Dictionary<string, string>(llm, StringComparer.OrdinalIgnoreCase);


                var toPersist = new List<SkillVocabularyEntry>(unknownOriginals.Count);
                foreach (var original in unknownOriginals)
                {
                    if (!batchResult.TryGetValue(original, out var syns)
                        || string.IsNullOrWhiteSpace(syns))
                    {


                        syns = IdentitySynonyms(original);
                        batchResult[original] = syns;
                    }
                    toPersist.Add(SkillVocabularyEntry.Create(
                        canonical: original,
                        synonymsJson: syns,
                        domain: domain,
                        source: "llm_batch",
                        modelVersion: _expander.Version));
                }

                try
                {
                    await _repo.UpsertBatchAsync(toPersist, ct);
                }
                catch (Exception ex)
                {


                    _logger.LogWarning(ex,
                        "Skill vocab: failed to persist {Count} batch entries — request proceeds",
                        toPersist.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Skill vocab: batch LLM call failed — falling back to identity for {Count} unknowns",
                    unknownLowers.Count);


                batchResult = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                foreach (var original in unknownOriginals)
                    batchResult[original] = IdentitySynonyms(original);
            }
        }


        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var (lower, original) in canonicalToOriginal)
        {
            if (existing.TryGetValue(lower, out var entry))
            {
                result[original] = entry.SynonymsJson;
                continue;
            }
            if (batchResult is not null && batchResult.TryGetValue(original, out var fresh))
            {
                result[original] = fresh;
                continue;
            }

            result[original] = IdentitySynonyms(original);
        }
        return result;
    }


    internal static string IdentitySynonyms(string skill)
    {
        var sb = new StringBuilder(64);
        sb.Append("[{\"term\":");
        sb.Append(JsonSerializer.Serialize(skill));
        sb.Append(",\"confidence\":1.0}]");
        return sb.ToString();
    }
}
