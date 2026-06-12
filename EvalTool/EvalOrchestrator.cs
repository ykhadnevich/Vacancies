using EvalTool.Grading;
using EvalTool.Pipeline;
using EvalTool.Reporting;
using Microsoft.Extensions.Logging;

namespace EvalTool;


public sealed class EvalOrchestrator
{
    private readonly BatchNormalizer _batchNormalizer;
    private readonly EvaluationEngine _evaluator;
    private readonly ReportWriter _reporter;
    private readonly ILogger<EvalOrchestrator> _logger;

    public EvalOrchestrator(
        BatchNormalizer batchNormalizer,
        EvaluationEngine evaluator,
        ReportWriter reporter,
        ILogger<EvalOrchestrator> logger)
    {
        _batchNormalizer = batchNormalizer;
        _evaluator = evaluator;
        _reporter = reporter;
        _logger = logger;
    }

    public async Task<double> RunAsync(
        string goldSetDir,
        string outputDir,
        string version,
        CancellationToken ct = default,
        int samples = 1)
    {
        var cvsDir = Path.Combine(goldSetDir, "cvs");
        var expectedDir = Path.Combine(goldSetDir, "expected");
        var normalizedDir = Path.Combine(outputDir, "normalized");

        if (!Directory.Exists(cvsDir))
            throw new DirectoryNotFoundException($"Gold-set CVs folder not found: {cvsDir}");
        if (!Directory.Exists(expectedDir))
            throw new DirectoryNotFoundException($"Gold-set expected folder not found: {expectedDir}");

        Directory.CreateDirectory(outputDir);
        Directory.CreateDirectory(normalizedDir);


        _logger.LogInformation("Phase 1: Normalize {Cvs} (samples per CV = {Samples})", cvsDir, samples);
        var normalized = await _batchNormalizer.NormalizeAllAsync(cvsDir, normalizedDir, ct, samples: samples);


        _logger.LogInformation("Phase 2: Grade {Count} normalized outputs", normalized.Count);
        var caseScores = new List<CaseScores>();
        foreach (var item in normalized)
        {
            var expectedPath = Path.Combine(expectedDir, item.CaseId + ".json");
            if (!File.Exists(expectedPath))
            {
                _logger.LogWarning("[Grade] {CaseId}: no expected JSON at {Path} — skipping grading",
                    item.CaseId, expectedPath);
                continue;
            }
            try
            {
                var expectedJson = await File.ReadAllTextAsync(expectedPath, ct);
                var scores = _evaluator.Grade(item.CaseId, item.ActualJson, expectedJson);
                caseScores.Add(scores);
                _logger.LogInformation("[Grade] {CaseId}: overall={Overall:F3}",
                    item.CaseId, scores.Overall);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Grade] {CaseId}: failed", item.CaseId);
            }
        }

        if (caseScores.Count == 0)
        {
            _logger.LogWarning("No cases graded. Did you populate gold_set/expected/?");
            return 0;
        }


        var report = _evaluator.Aggregate(version, caseScores);
        _reporter.Write(report, outputDir);


        long totalIn  = normalized.Sum(n => (long)n.InputTokens);
        long totalOut = normalized.Sum(n => (long)n.OutputTokens);
        long totalAll = totalIn + totalOut;


        const double InputUsdPerMillion = 0.30;
        const double OutputUsdPerMillion = 2.50;
        double estCost = (totalIn / 1_000_000.0) * InputUsdPerMillion
                       + (totalOut / 1_000_000.0) * OutputUsdPerMillion;


        var historyDir = Path.GetFullPath("eval_history");
        _reporter.AppendHistory(report, historyDir, totalIn, totalOut, estCost, outputDir);

        Console.WriteLine();
        Console.WriteLine($"== Run summary ({version}) ==");
        Console.WriteLine($"Cases normalized: {normalized.Count}");
        Console.WriteLine($"Cases graded:     {caseScores.Count}");
        Console.WriteLine($"Overall:          {report.Overall:F3}");
        Console.WriteLine();
        Console.WriteLine("== Token usage (Gemini) ==");
        Console.WriteLine($"Input tokens:     {totalIn:N0}");
        Console.WriteLine($"Output tokens:    {totalOut:N0}");
        Console.WriteLine($"Total tokens:     {totalAll:N0}");
        Console.WriteLine($"Estimated cost:   ${estCost:F4}");
        if (normalized.Count > 0)
        {
            Console.WriteLine(
                $"Per-CV avg:       input={totalIn / normalized.Count:N0}, " +
                $"output={totalOut / normalized.Count:N0}, " +
                $"cost=${estCost / normalized.Count:F4}");
        }
        Console.WriteLine();
        Console.WriteLine($"Reports: {outputDir}");
        return report.Overall;
    }
}
