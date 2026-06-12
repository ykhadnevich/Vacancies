using EvalTool.Pipeline;
using EvalTool.Reporting;
using Microsoft.Extensions.Logging;

namespace EvalTool;


public sealed class ScoringEvalOrchestrator
{
    private readonly BatchScoringEvaluator _batch;
    private readonly ReportWriter _reporter;
    private readonly ILogger<ScoringEvalOrchestrator> _logger;

    public ScoringEvalOrchestrator(
        BatchScoringEvaluator batch,
        ReportWriter reporter,
        ILogger<ScoringEvalOrchestrator> logger)
    {
        _batch = batch;
        _reporter = reporter;
        _logger = logger;
    }

    public async Task<double> RunAsync(
        string scoringResultsDir,
        string cvGoldDir,
        string vacancyGoldDir,
        string outputDir,
        string version,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Score eval] start. scoring={Scoring}, cv_gold={Cv}, vac_gold={Vac}, out={Out}",
            scoringResultsDir, cvGoldDir, vacancyGoldDir, outputDir);

        var perCase = await _batch.RunAsync(scoringResultsDir, cvGoldDir, vacancyGoldDir, ct);

        if (perCase.Count == 0)
        {
            _logger.LogWarning("No pairs graded — nothing to report.");
            return 0.0;
        }


        var allMetrics = perCase.SelectMany(c => c.FieldScores.Keys).Distinct().ToList();
        var perMetricAvg = allMetrics.ToDictionary(
            m => m,
            m =>
            {
                var vals = perCase
                    .Where(c => c.FieldScores.ContainsKey(m))
                    .Select(c => c.FieldScores[m])
                    .ToList();
                return vals.Count == 0 ? 0.0 : vals.Average();
            });


        var overall = perCase.Average(c => c.Overall);

        var report = new EvaluationReport(
            Version: version,
            RunAt: DateTime.UtcNow,
            PerCaseScores: perCase,
            PerFieldAverages: perMetricAvg,
            Overall: overall);

        _logger.LogInformation(
            "[Score eval] Graded {N} pairs. Overall (= 1 - mean MAE) = {Overall:F4}. " +
            "Mean MAE = {Mae:F4}, Verdict match = {VM:P1}",
            perCase.Count,
            overall,
            perMetricAvg.GetValueOrDefault("score.mae", 0.0),
            perMetricAvg.GetValueOrDefault("score.verdict_match", 0.0));

        Directory.CreateDirectory(outputDir);
        _reporter.Write(report, outputDir);
        _reporter.AppendHistory(
            report,
            historyDir: Path.Combine(outputDir, "..", "eval_history", "scoring"),
            inputTokens: 0,
            outputTokens: 0,
            estCostUsd: 0.0,
            runFolder: outputDir);

        return overall;
    }
}
