using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class BatchReasonGenerator
{
    private readonly IBatchedReasonService _reasonService;
    private readonly ILogger<BatchReasonGenerator> _logger;


    private const int ChunkSize = 10;

    public BatchReasonGenerator(
        IBatchedReasonService reasonService,
        ILogger<BatchReasonGenerator> logger)
    {
        _reasonService = reasonService;
        _logger = logger;
    }


    public async Task<BatchReasonStats> RunAsync(
        string scoringResultsDir,
        string vacancyNormalizedDir,
        string outputDir,
        CancellationToken ct = default,
        bool useLegacyV6Prompt = false)
    {


        if (_reasonService is Infrastructure.RelevancePipeline.V2.Scoring.GeminiBatchedReasonService gemSvc)
        {
            gemSvc.UseLegacyV6Prompt = useLegacyV6Prompt;
        }

        if (!Directory.Exists(scoringResultsDir))
            throw new DirectoryNotFoundException($"scoring-results dir not found: {scoringResultsDir}");
        if (!Directory.Exists(vacancyNormalizedDir))
            throw new DirectoryNotFoundException($"vacancy-normalized dir not found: {vacancyNormalizedDir}");

        Directory.CreateDirectory(outputDir);
        var start = DateTime.UtcNow;
        int totalRequested = 0, totalWritten = 0, failed = 0, skipped = 0, missingVac = 0;

        var jsonOpts = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        foreach (var cvDir in Directory.EnumerateDirectories(scoringResultsDir))
        {
            if (ct.IsCancellationRequested) break;
            var cvId = Path.GetFileName(cvDir);
            var cvOutDir = Path.Combine(outputDir, cvId);
            Directory.CreateDirectory(cvOutDir);


            var requests = new List<(BatchedReasonRequest req, string outPath)>();
            foreach (var scoreFile in Directory.EnumerateFiles(cvDir, "*.json"))
            {
                if (ct.IsCancellationRequested) break;
                var vacancyId = Path.GetFileNameWithoutExtension(scoreFile);
                var outPath = Path.Combine(cvOutDir, $"{vacancyId}.json");

                if (File.Exists(outPath) && IsValidJsonFile(outPath))
                {
                    skipped++;
                    continue;
                }

                try
                {
                    var scoring = JsonSerializer.Deserialize<ScoringResult>(
                        await File.ReadAllTextAsync(scoreFile, ct), jsonOpts);
                    if (scoring is null) { failed++; continue; }
                    if (scoring.Context is null)
                    {

                        _logger.LogDebug("Skipping {V}: ScoringResult.Context is null", vacancyId);
                        failed++;
                        continue;
                    }


                    var vacancyPath = Path.Combine(vacancyNormalizedDir, $"{vacancyId}.json");
                    if (!File.Exists(vacancyPath))
                    {
                        missingVac++;
                        continue;
                    }
                    var vacancyTitle = ExtractRoleTitleEn(await File.ReadAllTextAsync(vacancyPath, ct))
                                       ?? "Vacancy";

                    requests.Add((
                        new BatchedReasonRequest(
                            VacancyId: scoring.VacancyId,
                            VacancyTitle: vacancyTitle,
                            Verdict: scoring.Verdict,
                            Score: scoring.Score,
                            SubScores: scoring.SubScores,
                            Evidence: scoring.Evidence,
                            Context: scoring.Context),
                        outPath));
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to load scoring result {File}", scoreFile);
                    failed++;
                }
            }

            if (requests.Count == 0)
            {
                _logger.LogInformation("Pool {Cv}: nothing to do (skipped={S})", cvId, skipped);
                continue;
            }

            totalRequested += requests.Count;


            for (int i = 0; i < requests.Count; i += ChunkSize)
            {
                if (ct.IsCancellationRequested) break;
                var chunk = requests.Skip(i).Take(ChunkSize).ToList();
                IReadOnlyDictionary<Guid, BatchedReasonResult> results;
                try
                {
                    results = await _reasonService.GenerateBatchAsync(
                        chunk.Select(p => p.req).ToList(), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Batch reason call failed for CV={Cv} chunk@{I}", cvId, i);
                    failed += chunk.Count;
                    continue;
                }

                foreach (var (req, outPath) in chunk)
                {
                    if (!results.TryGetValue(req.VacancyId, out var r))
                    {
                        _logger.LogDebug("LLM dropped pair {V}", req.VacancyId);
                        failed++;
                        continue;
                    }


                    var verdictWordEn = VerdictEn(req.Verdict);
                    var verdictWordUk = VerdictUk(req.Verdict);
                    var reasonEn =
                        $"{verdictWordEn}. Strengths: {r.StrengthsEn} Gaps: {r.GapsEn} {r.RecommendationEn}";
                    var reasonUk =
                        $"{verdictWordUk}. Сильні сторони: {r.StrengthsUk} Прогалини: {r.GapsUk} {r.RecommendationUk}";

                    var output = new
                    {
                        cv_id = cvId,
                        vacancy_id = req.VacancyId.ToString(),
                        prompt_version = _reasonService.Version,
                        generated_at = DateTime.UtcNow,
                        verdict = req.Verdict.ToString(),
                        score = req.Score,
                        reason_en = reasonEn,
                        reason_uk = reasonUk,
                        strengths_en = r.StrengthsEn,
                        strengths_uk = r.StrengthsUk,
                        gaps_en = r.GapsEn,
                        gaps_uk = r.GapsUk,
                        recommendation_en = r.RecommendationEn,
                        recommendation_uk = r.RecommendationUk,
                        evidence = new
                        {
                            matched_skills = req.Evidence.MatchedSkills,
                            missing_must_haves = req.Evidence.MissingMustHaves,
                            triggered_anti_flags = req.Evidence.TriggeredAntiFlags
                        }
                    };

                    var bytes = System.Text.Encoding.UTF8.GetBytes(
                        JsonSerializer.Serialize(output, jsonOpts));
                    await using var fs = new FileStream(
                        outPath, FileMode.Create, FileAccess.Write, FileShare.None,
                        bufferSize: 4096, useAsync: true);
                    await fs.WriteAsync(bytes, ct);
                    await fs.FlushAsync(ct);
                    totalWritten++;
                }
            }

            _logger.LogInformation(
                "Pool {Cv}: requested={Req}, written={Wri}, failed={F}, skipped={S}",
                cvId, requests.Count, totalWritten, failed, skipped);
        }

        var elapsed = DateTime.UtcNow - start;
        _logger.LogInformation(
            "Batched reason gen done. Requested={Req}, Written={W}, Failed={F}, Skipped={S}, MissingVac={MV}, time={Min:F1}min",
            totalRequested, totalWritten, failed, skipped, missingVac, elapsed.TotalMinutes);

        return new BatchReasonStats(totalRequested, totalWritten, failed, skipped, missingVac, elapsed);
    }

    private static string? ExtractRoleTitleEn(string vacancyJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(vacancyJson);
            if (doc.RootElement.TryGetProperty("role_title", out var rt) &&
                rt.ValueKind == JsonValueKind.Object &&
                rt.TryGetProperty("en", out var en) &&
                en.ValueKind == JsonValueKind.String)
            {
                return en.GetString();
            }
            if (doc.RootElement.TryGetProperty("role_title_raw", out var raw) &&
                raw.ValueKind == JsonValueKind.String)
            {
                return raw.GetString();
            }
            return null;
        }
        catch { return null; }
    }

    private static string VerdictEn(Verdict v) => v switch
    {
        Verdict.StrongMatch  => "Strong match",
        Verdict.PartialMatch => "Partial match",
        Verdict.WeakMatch    => "Weak match",
        Verdict.Mismatch     => "Mismatch",
        _ => v.ToString()
    };

    private static string VerdictUk(Verdict v) => v switch
    {
        Verdict.StrongMatch  => "Сильна відповідність",
        Verdict.PartialMatch => "Часткова відповідність",
        Verdict.WeakMatch    => "Слабка відповідність",
        Verdict.Mismatch     => "Невідповідність",
        _ => v.ToString()
    };

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


public sealed record BatchReasonStats(
    int TotalRequested,
    int Written,
    int Failed,
    int Skipped,
    int MissingVacancy,
    System.TimeSpan Elapsed);
