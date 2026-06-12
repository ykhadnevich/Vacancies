using System.Globalization;
using System.Text;
using Microsoft.Extensions.Logging;

namespace EvalTool.Reporting;


public sealed class ReportWriter
{
    private readonly ILogger<ReportWriter> _logger;

    public ReportWriter(ILogger<ReportWriter> logger)
    {
        _logger = logger;
    }

    public void Write(EvaluationReport report, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        WritePerCaseMatrix(report, Path.Combine(outputDir, "per_case_per_metric.csv"));
        WritePerMetricSummary(report, Path.Combine(outputDir, "per_metric_summary.csv"));
        WriteMarkdownReport(report, Path.Combine(outputDir, "report.md"));
        _logger.LogInformation("Reports written to {Dir}", outputDir);
    }


    public void AppendHistory(
        EvaluationReport report,
        string historyDir,
        long inputTokens,
        long outputTokens,
        double estCostUsd,
        string runFolder)
    {
        Directory.CreateDirectory(historyDir);
        AppendCsvRow(report, Path.Combine(historyDir, "runs.csv"),
            inputTokens, outputTokens, estCostUsd, runFolder);
        AppendMarkdownBlock(report, Path.Combine(historyDir, "HISTORY.md"),
            inputTokens, outputTokens, estCostUsd, runFolder);
        _logger.LogInformation("History row appended to {Dir}", historyDir);
    }

    private static void AppendCsvRow(
        EvaluationReport report, string path,
        long inputTokens, long outputTokens, double estCost, string runFolder)
    {
        var metrics = report.PerFieldAverages.Keys.OrderBy(k => k).ToList();
        bool fileIsNew = !File.Exists(path);

        using var writer = new StreamWriter(path, append: true);
        if (fileIsNew)
        {
            writer.Write("timestamp,version,cases,overall,input_tokens,output_tokens,total_tokens,est_cost_usd,run_folder");
            foreach (var m in metrics) { writer.Write(','); writer.Write(Csv(m)); }
            writer.WriteLine();
        }

        writer.Write(report.RunAt.ToString("yyyy-MM-ddTHH:mm:ss", CultureInfo.InvariantCulture));
        writer.Write(','); writer.Write(Csv(report.Version));
        writer.Write(','); writer.Write(report.PerCaseScores.Count);
        writer.Write(','); writer.Write(report.Overall.ToString("F3", CultureInfo.InvariantCulture));
        writer.Write(','); writer.Write(inputTokens);
        writer.Write(','); writer.Write(outputTokens);
        writer.Write(','); writer.Write(inputTokens + outputTokens);
        writer.Write(','); writer.Write(estCost.ToString("F4", CultureInfo.InvariantCulture));
        writer.Write(','); writer.Write(Csv(runFolder));
        foreach (var m in metrics)
        {
            writer.Write(',');
            writer.Write(report.PerFieldAverages[m].ToString("F3", CultureInfo.InvariantCulture));
        }
        writer.WriteLine();
    }

    private static void AppendMarkdownBlock(
        EvaluationReport report, string path,
        long inputTokens, long outputTokens, double estCost, string runFolder)
    {
        bool fileIsNew = !File.Exists(path);
        using var writer = new StreamWriter(path, append: true);
        if (fileIsNew)
        {
            writer.WriteLine("# Eval history");
            writer.WriteLine();
            writer.WriteLine("Append-only log of every EvalTool run. Newest entry at the bottom.");
            writer.WriteLine();
        }

        writer.WriteLine($"## {report.RunAt:yyyy-MM-dd HH:mm} UTC — `{report.Version}` — overall **{report.Overall:F3}**");
        writer.WriteLine();
        writer.WriteLine($"- Cases graded: {report.PerCaseScores.Count}");
        writer.WriteLine($"- Tokens: input {inputTokens:N0}, output {outputTokens:N0}, total {inputTokens + outputTokens:N0}");
        writer.WriteLine($"- Estimated cost: ${estCost:F4}");
        writer.WriteLine($"- Run folder: `{runFolder}`");
        writer.WriteLine();
        writer.WriteLine("3 weakest metrics:");
        foreach (var (m, s) in report.PerFieldAverages.OrderBy(kv => kv.Value).Take(3))
            writer.WriteLine($"  - `{m}`: {s:F3}");
        writer.WriteLine("3 strongest metrics:");
        foreach (var (m, s) in report.PerFieldAverages.OrderByDescending(kv => kv.Value).Take(3))
            writer.WriteLine($"  - `{m}`: {s:F3}");
        writer.WriteLine();
    }


    private static void WritePerCaseMatrix(EvaluationReport report, string path)
    {
        var allFields = report.PerFieldAverages.Keys.OrderBy(k => k).ToList();
        var sb = new StringBuilder();
        sb.Append("case_id");
        foreach (var f in allFields) { sb.Append(','); sb.Append(Csv(f)); }
        sb.Append(",overall");
        sb.Append('\n');

        foreach (var c in report.PerCaseScores.OrderBy(c => c.CaseId))
        {
            sb.Append(Csv(c.CaseId));
            foreach (var f in allFields)
            {
                sb.Append(',');
                sb.Append(c.FieldScores.TryGetValue(f, out var v)
                    ? v.ToString("F3", CultureInfo.InvariantCulture)
                    : "");
            }
            sb.Append(',');
            sb.Append(c.Overall.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append('\n');
        }
        File.WriteAllText(path, sb.ToString());
    }


    private static void WritePerMetricSummary(EvaluationReport report, string path)
    {
        var sb = new StringBuilder();
        sb.Append("metric,score\n");
        foreach (var (metric, score) in report.PerFieldAverages.OrderBy(kv => kv.Value))
        {
            sb.Append(Csv(metric));
            sb.Append(',');
            sb.Append(score.ToString("F3", CultureInfo.InvariantCulture));
            sb.Append('\n');
        }
        sb.Append(Csv("OVERALL"));
        sb.Append(',');
        sb.Append(report.Overall.ToString("F3", CultureInfo.InvariantCulture));
        sb.Append('\n');
        File.WriteAllText(path, sb.ToString());
    }


    private static void WriteMarkdownReport(EvaluationReport report, string path)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Evaluation report: {report.Version}");
        sb.AppendLine();
        sb.AppendLine($"Run at: {report.RunAt:yyyy-MM-dd HH:mm:ss} UTC");
        sb.AppendLine($"Cases: {report.PerCaseScores.Count}");
        sb.AppendLine($"**Overall: {report.Overall:F3}**");
        sb.AppendLine();

        sb.AppendLine("## Per-metric averages (sorted weakest first)");
        sb.AppendLine();
        sb.AppendLine("| Metric | Score |");
        sb.AppendLine("|---|---|");
        foreach (var (metric, score) in report.PerFieldAverages.OrderBy(kv => kv.Value))
            sb.AppendLine($"| {metric} | {score:F3} |");
        sb.AppendLine();

        sb.AppendLine("## Per-case overall (sorted weakest first)");
        sb.AppendLine();
        sb.AppendLine("| Case | Overall |");
        sb.AppendLine("|---|---|");
        foreach (var c in report.PerCaseScores.OrderBy(c => c.Overall))
            sb.AppendLine($"| {c.CaseId} | {c.Overall:F3} |");
        sb.AppendLine();

        sb.AppendLine("## Top 3 weakest metrics — failing cases");
        sb.AppendLine();
        var top3 = report.PerFieldAverages.OrderBy(kv => kv.Value).Take(3);
        foreach (var (metric, avg) in top3)
        {
            sb.AppendLine($"### {metric} (avg {avg:F3})");
            sb.AppendLine();
            sb.AppendLine("| Case | Score |");
            sb.AppendLine("|---|---|");
            var failing = report.PerCaseScores
                .Where(c => c.FieldScores.ContainsKey(metric))
                .OrderBy(c => c.FieldScores[metric])
                .Take(5);
            foreach (var c in failing)
                sb.AppendLine($"| {c.CaseId} | {c.FieldScores[metric]:F3} |");
            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
    }

    private static string Csv(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
