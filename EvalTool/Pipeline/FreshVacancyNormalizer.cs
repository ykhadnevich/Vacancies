using System.Text.Json;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;

/// <summary>
/// Normalises *all* raw vacancies from a single raw-vacancies JSON file
/// directly into a per-vacancy normalized JSON directory. Differs from
/// <see cref="BatchVacancyNormalizer"/> in that it does NOT gate on the
/// <c>selected.json</c> filter: every job in the raw input is normalised.
///
/// Used by the Variant B (fresh-vacancy) Layer 6 expansion:
/// <list type="number">
///   <item>User runs <c>scrape --queries-file queries_nontech.txt --output fresh.json</c></item>
///   <item>User runs <c>normalize-fresh --raw fresh.json --output fresh_normalized/</c></item>
///   <item>Held-out pairing uses the new normalised vacancies for ratings
///         the production prompt has never seen.</item>
/// </list>
/// </summary>
public sealed class FreshVacancyNormalizer
{
    private readonly IVacancyExtractionService _normalizer;
    private readonly ILogger<FreshVacancyNormalizer> _logger;

    public FreshVacancyNormalizer(
        IVacancyExtractionService normalizer,
        ILogger<FreshVacancyNormalizer> logger)
    {
        _normalizer = normalizer;
        _logger = logger;
    }

    public async Task RunAsync(
        string rawPath,
        string outputDir,
        int? limit = null,
        string? dedupAgainstDir = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(rawPath))
            throw new FileNotFoundException("Raw vacancies file not found", rawPath);
        Directory.CreateDirectory(outputDir);

        var rawById = await LoadRawAsync(rawPath, ct);
        _logger.LogInformation("Loaded {Count} raw vacancies from {Path}", rawById.Count, rawPath);

        // Dedup against an existing normalised pool (e.g. the original 357).
        if (!string.IsNullOrEmpty(dedupAgainstDir) && Directory.Exists(dedupAgainstDir))
        {
            var existing = Directory.GetFiles(dedupAgainstDir, "*.json")
                .Select(p => Path.GetFileNameWithoutExtension(p))
                .ToHashSet();
            var before = rawById.Count;
            foreach (var dup in existing)
                rawById.Remove(dup);
            _logger.LogInformation(
                "Deduped against {Dir}: {Before} → {After} ({Removed} duplicates)",
                dedupAgainstDir, before, rawById.Count, before - rawById.Count);
        }

        var ids = rawById.Keys.ToList();
        if (limit is int n) ids = ids.Take(n).ToList();
        _logger.LogInformation("Will normalise {N} vacancies", ids.Count);

        int success = 0, failed = 0, skipped = 0;
        int totalIn = 0, totalOut = 0;
        var start = DateTime.UtcNow;

        foreach (var (i, id) in ids.Select((id, i) => (i, id)))
        {
            if (ct.IsCancellationRequested) break;

            var outPath = Path.Combine(outputDir, $"{id}.json");
            if (File.Exists(outPath)) { skipped++; continue; }

            try
            {
                var result = await _normalizer.ExtractAsync(rawById[id], ct);
                if (string.IsNullOrWhiteSpace(result.Json))
                {
                    failed++;
                    _logger.LogWarning("Empty normalization for {Id}", id);
                    continue;
                }
                await File.WriteAllTextAsync(outPath, result.Json, ct);
                totalIn += result.InputTokens;
                totalOut += result.OutputTokens;
                success++;
                if ((i + 1) % 20 == 0)
                    _logger.LogInformation(
                        "  Progress: {Done}/{Total} (success={Success}, failed={Failed}, skipped={Skipped})",
                        i + 1, ids.Count, success, failed, skipped);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Normalization failed for {Id}", id);
            }
        }

        var elapsed = DateTime.UtcNow - start;
        _logger.LogInformation(
            "Done. success={S} failed={F} skipped={K} elapsed={Min:F1}min tokens=in:{In}/out:{Out}",
            success, failed, skipped, elapsed.TotalMinutes, totalIn, totalOut);
    }

    private static async Task<Dictionary<string, string>> LoadRawAsync(
        string rawPath, CancellationToken ct)
    {
        var map = new Dictionary<string, string>();
        var text = await File.ReadAllTextAsync(rawPath, ct);
        using var doc = JsonDocument.Parse(text);

        // Scrape command writes a bare JSON array of vacancy objects at the root;
        // per-query files have a {"jobs": [...]} wrapper. Accept either shape.
        JsonElement jobs;
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            jobs = doc.RootElement;
        else if (doc.RootElement.ValueKind == JsonValueKind.Object
              && doc.RootElement.TryGetProperty("jobs", out var jobsProp))
            jobs = jobsProp;
        else
            return map;

        foreach (var job in jobs.EnumerateArray())
        {
            var id = job.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
            var title = job.TryGetProperty("title", out var tEl) ? tEl.GetString() : "";
            // Scrape command writes "raw_text"; per-query files write "description". Accept both.
            string? desc = null;
            if (job.TryGetProperty("raw_text", out var rEl) && rEl.ValueKind == JsonValueKind.String)
                desc = rEl.GetString();
            else if (job.TryGetProperty("description", out var dEl) && dEl.ValueKind == JsonValueKind.String)
                desc = dEl.GetString();
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (string.IsNullOrWhiteSpace(desc)) continue;
            map[id!] = $"Title: {title}\n\nDescription:\n{desc}";
        }
        return map;
    }
}
