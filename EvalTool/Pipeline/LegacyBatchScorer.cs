using System.Diagnostics;
using System.Text.Json;
using Infrastructure.RelevancePipeline.V2.Scoring.Legacy;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class LegacyBatchScorer
{
    private readonly LegacyScoringService _scoring;
    private readonly ILogger<LegacyBatchScorer> _logger;

    public LegacyBatchScorer(
        LegacyScoringService scoring,
        ILogger<LegacyBatchScorer> logger)
    {
        _scoring = scoring;
        _logger = logger;
    }

    public async Task<BatchScoringStats> RunAsync(
        string cvGoldDir,
        string rawVacanciesDir,
        string selectedJsonPath,
        string outputDir,
        CancellationToken ct = default)
    {
        if (!File.Exists(selectedJsonPath))
            throw new FileNotFoundException($"selected.json not found: {selectedJsonPath}");
        if (!Directory.Exists(rawVacanciesDir))
            throw new DirectoryNotFoundException($"raw vacancies dir not found: {rawVacanciesDir}");

        Directory.CreateDirectory(outputDir);

        var rawByGuid = await LoadRawVacancyFieldsByGuidAsync(rawVacanciesDir, ct);
        _logger.LogInformation("Loaded {Count} raw vacancies (title+company+description) into lookup", rawByGuid.Count);

        var selectedText = await File.ReadAllTextAsync(selectedJsonPath, ct);
        using var selDoc = JsonDocument.Parse(selectedText);
        if (!selDoc.RootElement.TryGetProperty("cv_pools", out var pools))
            throw new InvalidDataException("selected.json missing 'cv_pools'");

        var start = DateTime.UtcNow;
        int success = 0, failed = 0, skipped = 0, missingCv = 0, missingVac = 0;

        var latenciesCsv = Path.Combine(outputDir, "latencies.csv");
        await using var latencyWriter = new StreamWriter(latenciesCsv, append: false);
        await latencyWriter.WriteLineAsync("cv_id,vacancy_id,latency_ms,success");

        foreach (var poolKvp in pools.EnumerateObject())
        {
            var cvId = poolKvp.Name;
            var cvGoldPath = Path.Combine(cvGoldDir, $"{cvId}.json");
            if (!File.Exists(cvGoldPath))
            {
                _logger.LogWarning("CV gold not found for '{Cv}', skipping pool", cvId);
                missingCv++;
                continue;
            }

            var cvText = await File.ReadAllTextAsync(cvGoldPath, ct);
            var cvOutDir = Path.Combine(outputDir, cvId);
            Directory.CreateDirectory(cvOutDir);

            if (!poolKvp.Value.TryGetProperty("selected", out var selectedArr)) continue;

            int poolSize = 0;
            foreach (var item in selectedArr.EnumerateArray())
            {
                if (ct.IsCancellationRequested) break;
                if (!item.TryGetProperty("vacancy_id", out var vidEl)) continue;
                var vidStr = vidEl.GetString();
                if (string.IsNullOrWhiteSpace(vidStr)) continue;
                poolSize++;

                var outPath = Path.Combine(cvOutDir, $"{vidStr}.json");
                if (File.Exists(outPath) && IsValidJsonFile(outPath))
                {
                    skipped++;
                    continue;
                }

                if (!rawByGuid.TryGetValue(vidStr, out var vac))
                {
                    _logger.LogDebug("Raw vacancy missing for {V}", vidStr);
                    missingVac++;
                    continue;
                }

                if (!Guid.TryParse(vidStr, out var vid)) vid = Guid.NewGuid();

                var sw = Stopwatch.StartNew();
                try
                {
                    var result = await _scoring.ScoreAsync(
                        cvId, vid,
                        cvText,
                        vac.Title, vac.Company, vac.Description,
                        ct);
                    sw.Stop();

                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });

                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                    await using (var fs = new FileStream(
                        outPath, FileMode.Create, FileAccess.Write, FileShare.None,
                        bufferSize: 4096, useAsync: true))
                    {
                        await fs.WriteAsync(bytes, ct);
                        await fs.FlushAsync(ct);
                        fs.SetLength(bytes.LongLength);
                    }
                    success++;
                    await latencyWriter.WriteLineAsync($"{cvId},{vidStr},{sw.ElapsedMilliseconds},true");
                }
                catch (Exception ex)
                {
                    sw.Stop();
                    _logger.LogWarning(ex, "[legacy] scoring failed for CV={Cv} vacancy={V}", cvId, vidStr);
                    failed++;
                    await latencyWriter.WriteLineAsync($"{cvId},{vidStr},{sw.ElapsedMilliseconds},false");
                }
            }

            _logger.LogInformation(
                "[legacy] Pool {Cv}: {Success}/{Total} scored (skipped={Skipped}, missing_vac={Missing})",
                cvId, success, poolSize, skipped, missingVac);
        }

        var elapsed = DateTime.UtcNow - start;
        _logger.LogInformation(
            "[legacy] Batch scoring done. Success={S}, Failed={F}, Skipped={K}, missing_cv={MCv}, missing_vac={MV}, time={Min:F1}min",
            success, failed, skipped, missingCv, missingVac, elapsed.TotalMinutes);

        return new BatchScoringStats(
            success, failed, skipped, missingCv, missingVac, elapsed,
            InputTokens: 0, OutputTokens: 0, ReasonFallbackPairs: 0);
    }


    private static async Task<Dictionary<string, (string Title, string Company, string Description)>>
        LoadRawVacancyFieldsByGuidAsync(string rawDir, CancellationToken ct)
    {
        var lookup = new Dictionary<string, (string, string, string)>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(rawDir, "query_*.json"))
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try { text = await File.ReadAllTextAsync(file, ct); }
            catch { continue; }

            try
            {
                using var doc = JsonDocument.Parse(text);
                var root = doc.RootElement;
                if (root.ValueKind != JsonValueKind.Object) continue;
                if (!root.TryGetProperty("jobs", out var jobsEl)
                    || jobsEl.ValueKind != JsonValueKind.Array) continue;

                foreach (var job in jobsEl.EnumerateArray())
                {
                    if (job.ValueKind != JsonValueKind.Object) continue;
                    if (!job.TryGetProperty("id", out var idEl) || idEl.ValueKind != JsonValueKind.String) continue;
                    var id = idEl.GetString();
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    string title       = job.TryGetProperty("title",       out var t1) && t1.ValueKind == JsonValueKind.String ? t1.GetString() ?? "" : "";
                    string company     = job.TryGetProperty("company",     out var c1) && c1.ValueKind == JsonValueKind.String ? c1.GetString() ?? "" : "";
                    string description = job.TryGetProperty("description", out var d1) && d1.ValueKind == JsonValueKind.String ? d1.GetString() ?? "" : "";

                    if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description)) continue;
                    lookup[id] = (title, company, description);
                }
            }
            catch {  }
        }
        return lookup;
    }

    private static bool IsValidJsonFile(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var doc = JsonDocument.Parse(fs);
            return true;
        }
        catch { return false; }
    }
}
