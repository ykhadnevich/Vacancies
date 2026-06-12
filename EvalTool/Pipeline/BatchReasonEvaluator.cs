using System.Text.Json;
using EvalTool.Grading;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class BatchReasonEvaluator
{
    private readonly ReasonEvaluationEngine _engine;
    private readonly ILogger<BatchReasonEvaluator> _logger;

    public BatchReasonEvaluator(
        ReasonEvaluationEngine engine,
        ILogger<BatchReasonEvaluator> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task<List<CaseScores>> RunAsync(
        string scoringResultsDir,
        string cvGoldDir,
        string vacancyNormalizedDir,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(scoringResultsDir))
            throw new DirectoryNotFoundException($"Scoring results dir not found: {scoringResultsDir}");
        if (!Directory.Exists(cvGoldDir))
            throw new DirectoryNotFoundException($"CV gold dir not found: {cvGoldDir}");
        if (!Directory.Exists(vacancyNormalizedDir))
            throw new DirectoryNotFoundException($"Vacancy normalized dir not found: {vacancyNormalizedDir}");

        var results = new List<CaseScores>();
        var cvCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var processedPairs = 0;
        var failures = 0;

        var startTime = DateTime.UtcNow;

        foreach (var cvDir in Directory.EnumerateDirectories(scoringResultsDir))
        {
            var cvId = Path.GetFileName(cvDir);


            var cvGoldPath = Path.Combine(cvGoldDir, $"{cvId}.json");
            if (!cvCache.TryGetValue(cvId, out var cvJson))
            {
                if (!File.Exists(cvGoldPath))
                {
                    _logger.LogWarning("Skipping CV {Cv} — no gold file at {Path}", cvId, cvGoldPath);
                    continue;
                }
                cvJson = await File.ReadAllTextAsync(cvGoldPath, ct);
                cvCache[cvId] = cvJson;
            }

            foreach (var scoringFile in Directory.EnumerateFiles(cvDir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                var vacancyId = Path.GetFileNameWithoutExtension(scoringFile);
                var vacancyPath = Path.Combine(vacancyNormalizedDir, $"{vacancyId}.json");

                if (!File.Exists(vacancyPath))
                {
                    _logger.LogDebug(
                        "Skipping pair {Cv}×{Vac} — no normalized vacancy at {Path}",
                        cvId, vacancyId, vacancyPath);
                    continue;
                }

                try
                {
                    var scoringJson = await File.ReadAllTextAsync(scoringFile, ct);
                    var vacancyJson = await File.ReadAllTextAsync(vacancyPath, ct);
                    var caseId = $"{cvId}__{vacancyId}";

                    var caseScores = await _engine.GradeAsync(
                        caseId, scoringJson, cvJson, vacancyJson, ct);
                    results.Add(caseScores);
                    processedPairs++;

                    if (processedPairs % 25 == 0)
                    {
                        var elapsed = DateTime.UtcNow - startTime;
                        _logger.LogInformation(
                            "Reason-eval progress: {N} pairs in {Elapsed:mm\\:ss}",
                            processedPairs, elapsed);
                    }
                }
                catch (Exception ex)
                {
                    failures++;
                    _logger.LogError(ex,
                        "Reason-eval failed for {Cv}×{Vac}",
                        cvId, vacancyId);
                }
            }
        }

        var total = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Reason-eval done: {N} pairs graded, {F} failures, elapsed {Elapsed:mm\\:ss}",
            processedPairs, failures, total);

        return results;
    }
}
