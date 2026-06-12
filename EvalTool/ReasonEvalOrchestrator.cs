using EvalTool.Grading;
using EvalTool.Pipeline;
using EvalTool.Reporting;
using Microsoft.Extensions.Logging;

namespace EvalTool;


public sealed class ReasonEvalOrchestrator
{
    private readonly BatchReasonEvaluator _batch;
    private readonly ReportWriter _reporter;
    private readonly ILogger<ReasonEvalOrchestrator> _logger;

    public ReasonEvalOrchestrator(
        BatchReasonEvaluator batch,
        ReportWriter reporter,
        ILogger<ReasonEvalOrchestrator> logger)
    {
        _batch = batch;
        _reporter = reporter;
        _logger = logger;
    }

    public async Task<double> RunAsync(
        string scoringResultsDir,
        string cvGoldDir,
        string vacancyNormalizedDir,
        string outputDir,
        string version,
        CancellationToken ct = default)
    {
        _logger.LogInformation(
            "[Step D] Reason eval start. scoring={Scoring}, cv={Cv}, vac={Vac}, out={Out}",
            scoringResultsDir, cvGoldDir, vacancyNormalizedDir, outputDir);


        var perCase = await _batch.RunAsync(
            scoringResultsDir, cvGoldDir, vacancyNormalizedDir, ct);

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


        var aggregateKeys = allMetrics.Where(m => m != "reason.factuality_claims_extracted").ToList();
        var overall = aggregateKeys.Count == 0
            ? 0.0
            : aggregateKeys.Sum(m => perMetricAvg.GetValueOrDefault(m, 0.0)) / aggregateKeys.Count;

        var report = new EvaluationReport(
            Version: version,
            RunAt: DateTime.UtcNow,
            PerCaseScores: perCase,
            PerFieldAverages: perMetricAvg,
            Overall: overall);

        _logger.LogInformation(
            "[Step D] Graded {N} pairs. Overall reason quality = {Overall:F3}",
            perCase.Count, overall);


        Directory.CreateDirectory(outputDir);
        _reporter.Write(report, outputDir);
        _reporter.AppendHistory(
            report,
            historyDir: Path.Combine(outputDir, "..", "eval_history", "reason"),
            inputTokens: 0,
            outputTokens: 0,
            estCostUsd: 0.0,
            runFolder: outputDir);

        return overall;
    }
}
