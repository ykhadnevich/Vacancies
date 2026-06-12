using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Eval;


public sealed class FileSystemEvalDataSource : IEvalDataSource
{
    private const string VacancyDirPrefix = "vacancy_";
    private const string NormalizedSubdir = "normalized";

    private readonly string _cvGoldRoot;
    private readonly string _resultsRoot;
    private readonly ILogger<FileSystemEvalDataSource> _logger;

    public FileSystemEvalDataSource(IConfiguration cfg, ILogger<FileSystemEvalDataSource> logger)
    {
        _logger = logger;
        var goldRoot = cfg["Eval:GoldSetRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "gold_set");
        _cvGoldRoot = Path.GetFullPath(goldRoot);

        var resultsRoot = cfg["Eval:ResultsRoot"]
            ?? Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "results");
        _resultsRoot = Path.GetFullPath(resultsRoot);

        _logger.LogDebug("EvalDataSource: gold={Gold}, results={Results}",
            _cvGoldRoot, _resultsRoot);
    }

    public async Task<string?> GetCvSummaryAsync(string cvId, CancellationToken ct = default)
    {
        var path = Path.Combine(_cvGoldRoot, "expected", $"{cvId}.json");
        if (!File.Exists(path))
        {
            _logger.LogDebug("CV gold not found at {Path}", path);
            return null;
        }
        return await File.ReadAllTextAsync(path, ct);
    }

    public async Task<string?> GetVacancyAnalysisAsync(Guid vacancyId, CancellationToken ct = default)
    {
        if (!Directory.Exists(_resultsRoot)) return null;


        var newestVacancyDir = Directory.EnumerateDirectories(_resultsRoot, VacancyDirPrefix + "*")
            .OrderByDescending(d => d)
            .FirstOrDefault();
        if (newestVacancyDir is null) return null;

        var path = Path.Combine(newestVacancyDir, NormalizedSubdir, $"{vacancyId}.json");
        if (!File.Exists(path))
        {
            _logger.LogDebug("Vacancy normalized JSON not found at {Path}", path);
            return null;
        }
        return await File.ReadAllTextAsync(path, ct);
    }
}
