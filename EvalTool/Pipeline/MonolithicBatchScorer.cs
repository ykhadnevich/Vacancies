using System.Diagnostics;
using System.Text.Json;
using Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class MonolithicBatchScorer
{
    private readonly MonolithicScoringService _scoring;
    private readonly ILogger<MonolithicBatchScorer> _logger;


    // Conservative ceiling — Gemini 2.5 Flash Tier 1 advertises 1000 RPM but burst
    // limits and JSON-mode overhead are real. 10 concurrent calls give us ~5x speedup
    // without putting us at risk of 429s. Tune via env var EVAL_PARALLELISM if needed.
    private static int Parallelism =>
        int.TryParse(Environment.GetEnvironmentVariable("EVAL_PARALLELISM"), out var v) && v > 0
            ? v
            : 10;

    public MonolithicBatchScorer(
        MonolithicScoringService scoring,
        ILogger<MonolithicBatchScorer> logger)
    {
        _scoring = scoring;
        _logger = logger;
    }


    public async Task<BatchScoringStats> RunAsync(
        string cvGoldDir,
        string rawVacanciesDir,
        string selectedJsonPath,
        string outputDir,
        string promptVersion = "v1",
        CancellationToken ct = default)
    {
        if (!File.Exists(selectedJsonPath))
            throw new FileNotFoundException($"selected.json not found: {selectedJsonPath}");
        if (!Directory.Exists(rawVacanciesDir))
            throw new DirectoryNotFoundException($"raw vacancies dir not found: {rawVacanciesDir}");

        Directory.CreateDirectory(outputDir);


        var rawByGuid = await LoadRawDescriptionsByGuidAsync(rawVacanciesDir, ct);
        _logger.LogInformation("Loaded {Count} raw vacancies into lookup", rawByGuid.Count);

        var selectedText = await File.ReadAllTextAsync(selectedJsonPath, ct);
        using var selDoc = JsonDocument.Parse(selectedText);
        if (!selDoc.RootElement.TryGetProperty("cv_pools", out var pools))
            throw new InvalidDataException("selected.json missing 'cv_pools'");

        var start = DateTime.UtcNow;
        int success = 0, failed = 0, skipped = 0, missingCv = 0, missingVac = 0;
        long totalInputTokens = 0, totalOutputTokens = 0;


        var latenciesCsv = Path.Combine(outputDir, "latencies.csv");
        await using var latencyWriter = new StreamWriter(latenciesCsv, append: false);
        await latencyWriter.WriteLineAsync("cv_id,vacancy_id,latency_ms,success");

        using var semaphore = new SemaphoreSlim(Parallelism);
        var latencyLock = new SemaphoreSlim(1, 1);

        _logger.LogInformation("Monolithic batch scoring with parallelism={Par}", Parallelism);

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


            var tasks = new List<Task>();

            foreach (var item in selectedArr.EnumerateArray())
            {
                if (ct.IsCancellationRequested) break;
                if (!item.TryGetProperty("vacancy_id", out var vidEl)) continue;
                var vidStr = vidEl.GetString();
                if (string.IsNullOrWhiteSpace(vidStr)) continue;

                var outPath = Path.Combine(cvOutDir, $"{vidStr}.json");
                if (File.Exists(outPath) && IsValidJsonFile(outPath))
                {
                    Interlocked.Increment(ref skipped);
                    continue;
                }

                if (!rawByGuid.TryGetValue(vidStr, out var rawText) || string.IsNullOrWhiteSpace(rawText))
                {
                    _logger.LogDebug("Raw vacancy text missing for {V}", vidStr);
                    Interlocked.Increment(ref missingVac);
                    continue;
                }

                if (!Guid.TryParse(vidStr, out var vid))
                    vid = Guid.NewGuid();

                var localCvId  = cvId;
                var localVidStr = vidStr;
                var localVid    = vid;
                var localRawText = rawText;
                var localOutPath = outPath;

                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync(ct);
                    var sw = Stopwatch.StartNew();
                    try
                    {
                        var result = await _scoring.ScoreRawAsync(
                            localCvId, localVid, cvSummaryJson, localRawText, promptVersion, ct);
                        sw.Stop();

                        Interlocked.Add(ref totalInputTokens, result.InputTokens);
                        Interlocked.Add(ref totalOutputTokens, result.OutputTokens);

                        var json = JsonSerializer.Serialize(result, new JsonSerializerOptions
                        {
                            WriteIndented = true,
                            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
                        });

                        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
                        await using (var fs = new FileStream(
                            localOutPath, FileMode.Create, FileAccess.Write, FileShare.None,
                            bufferSize: 4096, useAsync: true))
                        {
                            await fs.WriteAsync(bytes, ct);
                            await fs.FlushAsync(ct);
                            fs.SetLength(bytes.LongLength);
                        }
                        Interlocked.Increment(ref success);

                        await latencyLock.WaitAsync(ct);
                        try
                        {
                            await latencyWriter.WriteLineAsync(
                                $"{localCvId},{localVidStr},{sw.ElapsedMilliseconds},true");
                        }
                        finally { latencyLock.Release(); }
                    }
                    catch (Exception ex)
                    {
                        sw.Stop();
                        _logger.LogWarning(ex, "Monolithic scoring failed for CV={Cv} vacancy={V}",
                            localCvId, localVidStr);
                        Interlocked.Increment(ref failed);

                        await latencyLock.WaitAsync(ct);
                        try
                        {
                            await latencyWriter.WriteLineAsync(
                                $"{localCvId},{localVidStr},{sw.ElapsedMilliseconds},false");
                        }
                        finally { latencyLock.Release(); }
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }, ct));
            }

            await Task.WhenAll(tasks);

            _logger.LogInformation(
                "[mono] Pool {Cv} done. Running totals — success={S}, failed={F}, skipped={K}, missing_vac={MV}",
                cvId, success, failed, skipped, missingVac);
        }

        var elapsed = DateTime.UtcNow - start;


        // gemini-2.5-flash pricing (free tier $0): $0.30 / 1M input, $2.50 / 1M output
        const double pricePerMillionInputUsd  = 0.30;
        const double pricePerMillionOutputUsd = 2.50;

        double inputCostUsd  = totalInputTokens  / 1_000_000.0 * pricePerMillionInputUsd;
        double outputCostUsd = totalOutputTokens / 1_000_000.0 * pricePerMillionOutputUsd;
        double totalCostUsd  = inputCostUsd + outputCostUsd;

        double perPairCostUsd = success > 0 ? totalCostUsd / success : 0;
        double perPairSeconds = success > 0 ? elapsed.TotalSeconds / success : 0;

        _logger.LogInformation(
            "[mono] Batch scoring done. Success={S}, Failed={F}, Skipped={K}, missing_cv={MCv}, missing_vac={MV}, time={Min:F1}min",
            success, failed, skipped, missingCv, missingVac, elapsed.TotalMinutes);
        _logger.LogInformation(
            "[mono] Tokens: input={InTok:N0}, output={OutTok:N0}",
            totalInputTokens, totalOutputTokens);
        _logger.LogInformation(
            "[mono] Cost: input=${InCost:F4}, output=${OutCost:F4}, TOTAL=${TotCost:F4} USD",
            inputCostUsd, outputCostUsd, totalCostUsd);
        _logger.LogInformation(
            "[mono] Per pair: ~${PerPair:F5} USD, ~{PerPairSec:F2}s",
            perPairCostUsd, perPairSeconds);


        var summaryPath = Path.Combine(outputDir, "batch_summary.txt");
        await File.WriteAllTextAsync(summaryPath,
            $"Batch summary ({DateTime.UtcNow:o})\n" +
            $"==============================\n" +
            $"Pairs scored:    {success}\n" +
            $"Failed:          {failed}\n" +
            $"Skipped:         {skipped}\n" +
            $"Missing vacancy: {missingVac}\n" +
            $"Missing CV:      {missingCv}\n" +
            $"Wall clock:      {elapsed.TotalMinutes:F2} min ({elapsed.TotalSeconds:F1} s)\n" +
            $"\n" +
            $"Input tokens:    {totalInputTokens:N0}\n" +
            $"Output tokens:   {totalOutputTokens:N0}\n" +
            $"\n" +
            $"Cost (gemini-2.5-flash):\n" +
            $"  Input:  ${inputCostUsd:F4}\n" +
            $"  Output: ${outputCostUsd:F4}\n" +
            $"  TOTAL:  ${totalCostUsd:F4}\n" +
            $"\n" +
            $"Per pair: ${perPairCostUsd:F5} / {perPairSeconds:F2}s\n",
            ct);

        return new BatchScoringStats(
            success, failed, skipped, missingCv, missingVac, elapsed,
            InputTokens:  (int)Math.Min(int.MaxValue, totalInputTokens),
            OutputTokens: (int)Math.Min(int.MaxValue, totalOutputTokens),
            ReasonFallbackPairs: 0);
    }


    private static async Task<Dictionary<string, string>> LoadRawDescriptionsByGuidAsync(
        string rawDir, CancellationToken ct)
    {
        var lookup = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var file in Directory.EnumerateFiles(rawDir, "query_*.json"))
        {
            ct.ThrowIfCancellationRequested();
            string text;
            try
            {
                text = await File.ReadAllTextAsync(file, ct);
            }
            catch
            {
                continue;
            }

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
                    if (!job.TryGetProperty("id", out var idEl)
                        || idEl.ValueKind != JsonValueKind.String) continue;
                    var id = idEl.GetString();
                    if (string.IsNullOrWhiteSpace(id)) continue;

                    string? description = null;
                    if (job.TryGetProperty("description", out var descEl)
                        && descEl.ValueKind == JsonValueKind.String)
                        description = descEl.GetString();


                    if (string.IsNullOrWhiteSpace(description)
                        && job.TryGetProperty("title", out var titleEl)
                        && titleEl.ValueKind == JsonValueKind.String)
                        description = titleEl.GetString();

                    if (!string.IsNullOrWhiteSpace(description))
                        lookup[id] = description!;
                }
            }
            catch
            {

            }
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
