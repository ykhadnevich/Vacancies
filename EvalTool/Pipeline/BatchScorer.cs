using System.Text.Json;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class BatchScorer
{
    private readonly IScoringService _scoringService;
    private readonly ILogger<BatchScorer> _logger;

    public BatchScorer(IScoringService scoringService, ILogger<BatchScorer> logger)
    {
        _scoringService = scoringService;
        _logger = logger;
    }


    public async Task<BatchScoringStats> RunAsync(
        string cvGoldDir,
        string vacancyNormalizedDir,
        string selectedJsonPath,
        string outputDir,
        CancellationToken ct = default,
        bool skipJudge = false,
        bool skipReason = false)
    {
        if (!File.Exists(selectedJsonPath))
            throw new FileNotFoundException($"selected.json not found: {selectedJsonPath}");

        Directory.CreateDirectory(outputDir);

        var selectedText = await File.ReadAllTextAsync(selectedJsonPath, ct);
        using var selDoc = JsonDocument.Parse(selectedText);
        if (!selDoc.RootElement.TryGetProperty("cv_pools", out var pools))
            throw new InvalidDataException("selected.json missing 'cv_pools'");

        var start = DateTime.UtcNow;
        int success = 0, failed = 0, skipped = 0, missingCv = 0, missingVac = 0;
        long totalInputTokens = 0, totalOutputTokens = 0;
        int reasonFallbackCount = 0;

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

            var cvSummaryJson = await File.ReadAllTextAsync(cvGoldPath, ct);
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
                if (File.Exists(outPath))
                {


                    if (IsValidJsonFile(outPath))
                    {
                        skipped++;
                        continue;
                    }
                    _logger.LogWarning("Existing file is invalid JSON, will overwrite: {Path}", outPath);
                }

                var vacancyPath = Path.Combine(vacancyNormalizedDir, $"{vidStr}.json");
                if (!File.Exists(vacancyPath))
                {
                    _logger.LogDebug("Vacancy normalized JSON missing: {V}", vidStr);
                    missingVac++;
                    continue;
                }

                try
                {
                    var vacancyJson = await File.ReadAllTextAsync(vacancyPath, ct);
                    if (!Guid.TryParse(vidStr, out var vid))
                    {

                        vid = Guid.NewGuid();
                    }
                    var result = await _scoringService.ScoreAsync(
                        cvId, vid, cvSummaryJson, vacancyJson, ct,
                        skipReason: skipReason,
                        skipJudge: skipJudge);

                    totalInputTokens  += result.InputTokens;
                    totalOutputTokens += result.OutputTokens;


                    if (result.InputTokens == 0 && result.OutputTokens == 0)
                        reasonFallbackCount++;


                    var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                    });


                    var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                    await using (var fs = new FileStream(
                        outPath,
                        FileMode.Create,
                        FileAccess.Write,
                        FileShare.None,
                        bufferSize: 4096,
                        useAsync: true))
                    {
                        await fs.WriteAsync(bytes, ct);
                        await fs.FlushAsync(ct);
                        fs.SetLength(bytes.LongLength);
                    }
                    success++;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Scoring failed for CV={Cv} vacancy={V}", cvId, vidStr);
                    failed++;
                }
            }

            _logger.LogInformation(
                "Pool {Cv}: {Success}/{Total} scored (skipped={Skipped}, missing_vac={Missing})",
                cvId, success, poolSize, skipped, missingVac);
        }

        var elapsed = DateTime.UtcNow - start;
        _logger.LogInformation(
            "Batch scoring done. Success={S}, Failed={F}, Skipped={K}, missing_cv={MCv}, missing_vac={MV}, time={Min:F1}min, tokens=in:{IT:N0}/out:{OT:N0}, fallback={FB}",
            success, failed, skipped, missingCv, missingVac, elapsed.TotalMinutes,
            totalInputTokens, totalOutputTokens, reasonFallbackCount);

        return new BatchScoringStats(
            success, failed, skipped, missingCv, missingVac, elapsed,
            totalInputTokens, totalOutputTokens, reasonFallbackCount);
    }


    private static bool IsValidJsonFile(string path)
    {
        try
        {
            using var fs = File.OpenRead(path);
            using var doc = JsonDocument.Parse(fs);
            return true;
        }
        catch
        {
            return false;
        }
    }
}


public sealed record BatchScoringStats(
    int Success,
    int Failed,
    int Skipped,
    int MissingCv,
    int MissingVac,
    System.TimeSpan Elapsed,
    long InputTokens,
    long OutputTokens,
    int ReasonFallbackPairs);
