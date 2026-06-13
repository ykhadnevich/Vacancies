using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Entities;
using Domain.Interfaces.Services;
using Infrastructure.JobSources.Scraping;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;


public class VacancyScrapeRunner
{
    private readonly WorkUaScraperService _workUa;
    private readonly IEnumerable<IJobSourceService> _allSources;
    private readonly ILogger<VacancyScrapeRunner> _logger;

    public VacancyScrapeRunner(
        WorkUaScraperService workUa,
        IEnumerable<IJobSourceService> allSources,
        ILogger<VacancyScrapeRunner> logger)
    {
        _workUa = workUa;
        _allSources = allSources;
        _logger = logger;
    }

    public async Task RunAsync(
        string queriesFilePath,
        string outputPath,
        int maxPerQuery,
        bool useAllSources = false,
        CancellationToken ct = default)
    {
        if (!File.Exists(queriesFilePath))
            throw new FileNotFoundException($"Queries file not found: {queriesFilePath}");

        var queries = (await File.ReadAllLinesAsync(queriesFilePath, ct))
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && !l.StartsWith("#"))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        _logger.LogInformation("Loaded {Count} unique queries from {Path}",
            queries.Count, queriesFilePath);

        var byUrl = new Dictionary<string, ScrapedVacancyDto>(StringComparer.OrdinalIgnoreCase);
        var startedAt = DateTime.UtcNow;
        const int safetyPageCap = 200;

        foreach (var query in queries)
        {
            _logger.LogInformation("[QUERY] {Query}", query);
            int newThisQuery = 0;
            int page = 1;

            while (newThisQuery < maxPerQuery && page <= safetyPageCap)
            {
                IReadOnlyList<JobVacancy> jobs;
                try
                {
                    jobs = await _workUa.FetchJobsPageAsync(query, page, ct);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "  page {Page} failed for '{Query}', stopping pagination",
                        page, query);
                    break;
                }

                if (jobs.Count == 0)
                {
                    _logger.LogInformation("  page {Page}: empty, end of results", page);
                    break;
                }

                int newCount = 0;
                foreach (var job in jobs)
                {
                    var url = job.PrimaryUrl;
                    if (string.IsNullOrEmpty(url)) continue;

                    if (byUrl.TryGetValue(url, out var existing))
                    {
                        if (!existing.SearchQueries.Contains(query))
                            existing.SearchQueries.Add(query);
                        continue;
                    }

                    byUrl[url] = new ScrapedVacancyDto
                    {
                        Id = ExtractId(url),
                        Url = url,
                        Source = "work.ua",
                        SearchQueries = new List<string> { query },
                        Title = job.Title,
                        Company = string.IsNullOrEmpty(job.Company) ? null : job.Company,
                        RawText = string.IsNullOrEmpty(job.Description) ? null : job.Description,
                        Language = DetectLanguage(job.Description ?? job.Title),
                        PublishedAt = job.PublishedAt,
                        ScrapedAt = DateTime.UtcNow
                    };
                    newCount++;
                    newThisQuery++;
                    if (newThisQuery >= maxPerQuery) break;
                }

                _logger.LogInformation(
                    "  page {Page}: +{New} new, query total {QueryTotal}, all unique {AllUnique}",
                    page, newCount, newThisQuery, byUrl.Count);

                page++;
            }


            if (useAllSources)
            {
                foreach (var src in _allSources)
                {
                    if (string.Equals(src.SourceName, "work.ua", StringComparison.OrdinalIgnoreCase))
                        continue;

                    IReadOnlyList<JobVacancy> srcJobs;
                    try
                    {
                        srcJobs = await src.FetchJobsAsync(query, null, ct: ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "  [{Source}] fetch failed for '{Query}', skipping",
                            src.SourceName, query);
                        continue;
                    }

                    int addedFromSrc = 0;
                    foreach (var job in srcJobs)
                    {
                        var url = job.PrimaryUrl;
                        if (string.IsNullOrEmpty(url)) continue;

                        if (byUrl.TryGetValue(url, out var existing))
                        {
                            if (!existing.SearchQueries.Contains(query))
                                existing.SearchQueries.Add(query);
                            continue;
                        }

                        byUrl[url] = ScrapedToDomainMapper.FromDomain(job, query);
                        addedFromSrc++;
                    }

                    _logger.LogInformation(
                        "  [{Source}] +{Added} new (all unique now {Total})",
                        src.SourceName, addedFromSrc, byUrl.Count);
                }
            }
        }

        var elapsed = DateTime.UtcNow - startedAt;
        _logger.LogInformation("Done. Unique vacancies: {Count}. Elapsed: {Min:F1} min",
            byUrl.Count, elapsed.TotalMinutes);

        var all = byUrl.Values
            .OrderBy(v => v.SearchQueries.First())
            .ThenBy(v => v.Url, StringComparer.Ordinal)
            .ToList();

        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir))
            Directory.CreateDirectory(outDir);

        var jsonOpts = new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(all, jsonOpts);
        await File.WriteAllTextAsync(outputPath, json, ct);

        _logger.LogInformation("Wrote {Count} vacancies to {Path}", all.Count, outputPath);
    }

    private static string ExtractId(string url)
    {


        var match = System.Text.RegularExpressions.Regex.Match(url, @"/jobs/(\d+)/?");
        return match.Success
            ? $"workua_{match.Groups[1].Value}"
            : $"workua_{Math.Abs(url.GetHashCode())}";
    }

    private static string DetectLanguage(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return "unknown";
        int cyrillic = 0, latin = 0;
        foreach (var c in text)
        {
            if ((c >= 'А' && c <= 'я')
                || c == 'і' || c == 'І'
                || c == 'ї' || c == 'Ї'
                || c == 'є' || c == 'Є'
                || c == 'ґ' || c == 'Ґ')
                cyrillic++;
            else if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
                latin++;
        }
        if (cyrillic + latin == 0) return "unknown";
        if (cyrillic > latin * 2) return "uk";
        if (latin > cyrillic * 2) return "en";
        return "mixed";
    }
}
