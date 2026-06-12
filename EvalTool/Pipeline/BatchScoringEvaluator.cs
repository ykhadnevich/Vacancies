using EvalTool.Grading;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public sealed class BatchScoringEvaluator
{
    private readonly ScoringEvaluationEngine _engine;
    private readonly ILogger<BatchScoringEvaluator> _logger;

    public BatchScoringEvaluator(
        ScoringEvaluationEngine engine,
        ILogger<BatchScoringEvaluator> logger)
    {
        _engine = engine;
        _logger = logger;
    }

    public async Task<List<CaseScores>> RunAsync(
        string scoringResultsDir,
        string cvGoldDir,
        string vacancyGoldDir,
        CancellationToken ct = default)
    {
        if (!Directory.Exists(scoringResultsDir))
            throw new DirectoryNotFoundException($"Scoring results dir not found: {scoringResultsDir}");
        if (!Directory.Exists(cvGoldDir))
            throw new DirectoryNotFoundException($"CV gold dir not found: {cvGoldDir}");
        if (!Directory.Exists(vacancyGoldDir))
            throw new DirectoryNotFoundException($"Vacancy gold dir not found: {vacancyGoldDir}");

        var results = new List<CaseScores>();
        var cvCache = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var processed = 0;
        var skippedNoGold = 0;
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
                    _logger.LogWarning("CV {Cv} has no gold normalization at {Path} — skipping pool", cvId, cvGoldPath);
                    skippedNoGold += Directory.GetFiles(cvDir, "*.json").Length;
                    continue;
                }
                cvJson = await File.ReadAllTextAsync(cvGoldPath, ct);
                cvCache[cvId] = cvJson;
            }

            foreach (var scoringFile in Directory.EnumerateFiles(cvDir, "*.json"))
            {
                ct.ThrowIfCancellationRequested();
                var vacId = Path.GetFileNameWithoutExtension(scoringFile);
                var vacGoldPath = Path.Combine(vacancyGoldDir, $"{vacId}.json");

                if (!File.Exists(vacGoldPath))
                {
                    skippedNoGold++;
                    continue;
                }

                try
                {
                    var scoringJson = await File.ReadAllTextAsync(scoringFile, ct);
                    var vacJson = await File.ReadAllTextAsync(vacGoldPath, ct);
                    var caseId = $"{cvId}__{vacId}";

                    var caseScores = _engine.Grade(caseId, cvJson, vacJson, scoringJson);
                    results.Add(caseScores);
                    processed++;
                }
                catch (Exception ex)
                {
                    failures++;
                    _logger.LogError(ex, "Score eval failed for {Cv}×{Vac}", cvId, vacId);
                }
            }
        }

        var elapsed = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Score eval done: {N} pairs graded, {SkipNG} skipped (no gold), {F} failures, elapsed {Elapsed:mm\\:ss}",
            processed, skippedNoGold, failures, elapsed);

        return results;
    }
}
