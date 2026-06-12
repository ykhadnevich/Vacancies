using System.Text.Json;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class BatchVacancyNormalizer
{
    private readonly IVacancyExtractionService _normalizer;
    private readonly ILogger<BatchVacancyNormalizer> _logger;

    public BatchVacancyNormalizer(
        IVacancyExtractionService normalizer,
        ILogger<BatchVacancyNormalizer> logger)
    {
        _normalizer = normalizer;
        _logger = logger;
    }


    public async Task<BatchNormalizationStats> RunAsync(
        string goldRoot,
        string outputDir,
        CancellationToken ct = default)
    {
        var rawDir = Path.Combine(goldRoot, "vacancies", "raw");
        var selectedPath = Path.Combine(goldRoot, "vacancies", "selected", "selected.json");

        if (!Directory.Exists(rawDir))
            throw new DirectoryNotFoundException($"Raw vacancies dir missing: {rawDir}");
        if (!File.Exists(selectedPath))
            throw new FileNotFoundException($"selected.json missing: {selectedPath}");

        Directory.CreateDirectory(outputDir);


        var rawById = await LoadRawVacanciesAsync(rawDir, ct);
        _logger.LogInformation("Loaded {Count} raw vacancies from {Dir}", rawById.Count, rawDir);


        var selectedIds = await LoadSelectedIdsAsync(selectedPath, ct);
        _logger.LogInformation("Selection contains {Count} unique vacancies", selectedIds.Count);

        int success = 0, failed = 0, skipped = 0;
        int totalInputTokens = 0, totalOutputTokens = 0;
        var start = DateTime.UtcNow;

        int processed = 0;
        foreach (var vid in selectedIds)
        {
            if (ct.IsCancellationRequested) break;
            processed++;

            var outPath = Path.Combine(outputDir, $"{vid}.json");
            if (File.Exists(outPath))
            {
                skipped++;
                continue;
            }

            if (!rawById.TryGetValue(vid, out var raw) || string.IsNullOrWhiteSpace(raw))
            {
                _logger.LogWarning("Vacancy {Id} has no raw text in any query file — skipping", vid);
                failed++;
                continue;
            }

            try
            {
                var result = await _normalizer.ExtractAsync(raw, ct);
                if (string.IsNullOrWhiteSpace(result.Json))
                {
                    failed++;
                    _logger.LogWarning("Empty normalization for {Id}", vid);
                    continue;
                }

                await File.WriteAllTextAsync(outPath, result.Json, ct);
                totalInputTokens += result.InputTokens;
                totalOutputTokens += result.OutputTokens;
                success++;

                if (processed % 20 == 0)
                    _logger.LogInformation(
                        "  Progress: {Done}/{Total} (success={Success}, failed={Failed}, skipped={Skipped})",
                        processed, selectedIds.Count, success, failed, skipped);
            }
            catch (Exception ex)
            {
                failed++;
                _logger.LogError(ex, "Normalization failed for {Id}", vid);
            }
        }

        var elapsed = DateTime.UtcNow - start;
        _logger.LogInformation(
            "Batch done. Success={S}, Failed={F}, Skipped={K}, Elapsed={Min:F1}min, " +
            "Tokens: in={In}, out={Out}",
            success, failed, skipped, elapsed.TotalMinutes,
            totalInputTokens, totalOutputTokens);

        return new BatchNormalizationStats(success, failed, skipped,
            totalInputTokens, totalOutputTokens, elapsed);
    }


    private static async Task<Dictionary<string, string>> LoadRawVacanciesAsync(
        string rawDir, CancellationToken ct)
    {
        var map = new Dictionary<string, string>();
        foreach (var file in Directory.GetFiles(rawDir, "query_*.json"))
        {
            var text = await File.ReadAllTextAsync(file, ct);
            using var doc = JsonDocument.Parse(text);
            if (!doc.RootElement.TryGetProperty("jobs", out var jobs)) continue;
            foreach (var job in jobs.EnumerateArray())
            {
                var id = job.TryGetProperty("id", out var idEl) ? idEl.GetString() : null;
                var title = job.TryGetProperty("title", out var tEl) ? tEl.GetString() : "";
                var desc = job.TryGetProperty("description", out var dEl) ? dEl.GetString() : "";
                if (string.IsNullOrWhiteSpace(id)) continue;


                var combined = $"Title: {title}\n\nDescription:\n{desc}";
                map[id!] = combined;
            }
        }
        return map;
    }


    private static async Task<HashSet<string>> LoadSelectedIdsAsync(
        string selectedPath, CancellationToken ct)
    {
        var text = await File.ReadAllTextAsync(selectedPath, ct);
        using var doc = JsonDocument.Parse(text);
        var ids = new HashSet<string>();
        if (!doc.RootElement.TryGetProperty("cv_pools", out var pools))
            return ids;
        foreach (var poolKvp in pools.EnumerateObject())
        {
            if (!poolKvp.Value.TryGetProperty("selected", out var selectedArr)) continue;
            foreach (var sel in selectedArr.EnumerateArray())
            {
                if (sel.TryGetProperty("vacancy_id", out var idEl)
                    && idEl.GetString() is { } id)
                    ids.Add(id);
            }
        }
        return ids;
    }
}


public sealed record BatchNormalizationStats(
    int Success,
    int Failed,
    int Skipped,
    int InputTokens,
    int OutputTokens,
    System.TimeSpan Elapsed);
