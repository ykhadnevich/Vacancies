using EvalTool.Grading;
using EvalTool.Pipeline;
using EvalTool.Reporting;
using Microsoft.Extensions.Logging;

namespace EvalTool;


public sealed class VacancyEvalOrchestrator
{
    private readonly BatchVacancyNormalizer _batchNormalizer;
    private readonly VacancyEvaluationEngine _engine;
    private readonly ReportWriter _reportWriter;
    private readonly ILogger<VacancyEvalOrchestrator> _logger;

    public VacancyEvalOrchestrator(
        BatchVacancyNormalizer batchNormalizer,
        VacancyEvaluationEngine engine,
        ReportWriter reportWriter,
        ILogger<VacancyEvalOrchestrator> logger)
    {
        _batchNormalizer = batchNormalizer;
        _engine = engine;
        _reportWriter = reportWriter;
        _logger = logger;
    }

    public async Task<double> RunAsync(
        string goldRoot,
        string outputDir,
        string version,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(outputDir);
        var normalizedDir = Path.Combine(outputDir, "normalized");
        var expectedDir = Path.Combine(goldRoot, "vacancies", "expected");

        if (!Directory.Exists(expectedDir))
            throw new DirectoryNotFoundException(
                $"Expected (gold) dir missing: {expectedDir}");


        _logger.LogInformation("[Phase 1/3] Batch normalize → {Dir}", normalizedDir);
        var stats = await _batchNormalizer.RunAsync(goldRoot, normalizedDir, ct);


        _logger.LogInformation("[Phase 2/3] Grade against {Dir}", expectedDir);
        var perCase = new List<CaseScores>();
        foreach (var actualPath in Directory.GetFiles(normalizedDir, "*.json"))
        {
            var vid = Path.GetFileNameWithoutExtension(actualPath);
            var expectedPath = Path.Combine(expectedDir, $"{vid}.json");
            if (!File.Exists(expectedPath))
            {
                _logger.LogDebug("No expected JSON for {Id} — skipping", vid);
                continue;
            }
            try
            {
                var actualJson = await File.ReadAllTextAsync(actualPath, ct);
                var expectedJson = await File.ReadAllTextAsync(expectedPath, ct);
                var scores = _engine.Grade(vid, actualJson, expectedJson);
                perCase.Add(scores);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Grading failed for {Id}", vid);
            }
        }

        var report = _engine.Aggregate(version, perCase);
        _logger.LogInformation(
            "Graded {Count} cases. Overall = {Overall:F3}",
            perCase.Count, report.Overall);


        _logger.LogInformation("[Phase 3/3] Write reports → {Dir}", outputDir);
        _reportWriter.Write(report, outputDir);


        var estCost = (stats.InputTokens / 1_000_000.0) * 0.30
                    + (stats.OutputTokens / 1_000_000.0) * 2.50;


        var historyDir = ResolveHistoryDir(outputDir);
        _reportWriter.AppendHistory(
            report, historyDir,
            stats.InputTokens, stats.OutputTokens, estCost, outputDir);

        _logger.LogInformation(
            "Run complete. Overall F1 = {Overall:F3}. Cost ≈ ${Cost:F4}. " +
            "Time = {Min:F1}min.",
            report.Overall, estCost, stats.Elapsed.TotalMinutes);

        return report.Overall;
    }


    private static string ResolveHistoryDir(string outputDir)
    {
        var dir = outputDir;
        for (int i = 0; i < 8; i++)
        {
            dir = Path.GetDirectoryName(dir);
            if (string.IsNullOrEmpty(dir)) break;

            if (Directory.Exists(Path.Combine(dir, "EvalTool")))
                return Path.Combine(dir, "eval_history", "vacancy");
        }
        return Path.Combine(Path.GetDirectoryName(outputDir) ?? ".",
            "eval_history", "vacancy");
    }
}
