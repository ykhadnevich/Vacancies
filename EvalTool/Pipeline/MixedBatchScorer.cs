using System.Diagnostics;
using System.Text.Json;
using Infrastructure.RelevancePipeline.V2.Scoring.Mixed;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class MixedBatchScorer
{
    private readonly MixedScoringService _scoring;
    private readonly ILogger<MixedBatchScorer> _logger;

    public MixedBatchScorer(MixedScoringService scoring, ILogger<MixedBatchScorer> logger)
    {
        _scoring = scoring;
        _logger = logger;
    }

    public async Task<BatchScoringStats> RunAsync(
        string rawCvDir,
        string normalizedVacancyDir,
        string selectedJsonPath,
        string outputDir,
        CancellationToken ct = default)
    {
        if (!File.Exists(selectedJsonPath))
            throw new FileNotFoundException($"selected.json not found: {selectedJsonPath}");
        if (!Directory.Exists(rawCvDir))
            throw new DirectoryNotFoundException($"raw CV dir not found: {rawCvDir}");
        if (!Directory.Exists(normalizedVacancyDir))
            throw new DirectoryNotFoundException($"normalized vacancy dir not found: {normalizedVacancyDir}");

        Directory.CreateDirectory(outputDir);

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
            var rawCvPath = Path.Combine(rawCvDir, $"{cvId}.txt");
            if (!File.Exists(rawCvPath))
            {
                _logger.LogWarning("Raw CV text not found for '{Cv}' at {Path}, skipping pool", cvId, rawCvPath);
                missingCv++;
                continue;
            }

            var rawCvText = await File.ReadAllTextAsync(rawCvPath, ct);
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

                var vacancyPath = Path.Combine(normalizedVacancyDir, $"{vidStr}.json");
                if (!File.Exists(vacancyPath))
                {
                    _logger.LogDebug("Normalized vacancy JSON missing: {V}", vidStr);
                    missingVac++;
                    continue;
                }

                if (!Guid.TryParse(vidStr, out var vid)) vid = Guid.NewGuid();

                var sw = Stopwatch.StartNew();
                try
                {
                    var vacancyJson = await File.ReadAllTextAsync(vacancyPath, ct);
                    var result = await _scoring.ScoreAsync(cvId, vid, rawCvText, vacancyJson, ct);
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
                    _logger.LogWarning(ex, "[mixed] scoring failed for CV={Cv} vacancy={V}", cvId, vidStr);
                    failed++;
                    await latencyWriter.WriteLineAsync($"{cvId},{vidStr},{sw.ElapsedMilliseconds},false");
                }
            }

            _logger.LogInformation(
                "[mixed] Pool {Cv}: {Success}/{Total} scored (skipped={Skipped}, missing_vac={Missing})",
                cvId, success, poolSize, skipped, missingVac);
        }

        var elapsed = DateTime.UtcNow - start;
        _logger.LogInformation(
            "[mixed] Batch scoring done. Success={S}, Failed={F}, Skipped={K}, missing_cv={MCv}, missing_vac={MV}, time={Min:F1}min",
            success, failed, skipped, missingCv, missingVac, elapsed.TotalMinutes);

        return new BatchScoringStats(
            success, failed, skipped, missingCv, missingVac, elapsed,
            InputTokens: 0, OutputTokens: 0, ReasonFallbackPairs: 0);
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
