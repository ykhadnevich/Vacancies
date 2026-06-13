using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Infrastructure.JobAggregation;


public sealed class JobAggregationService : IJobAggregationService
{
    private readonly IEnumerable<IJobSourceService> _sources;
    private readonly IDeduplicationService _deduplication;
    private readonly IJobVacancyRepository _repo;
    private readonly IMemoryCache _cache;
    private readonly ILogger<JobAggregationService> _logger;


    private static readonly TimeSpan ScrapeCacheTtl = TimeSpan.FromMinutes(30);
    private static readonly TimeSpan EmptyResultCacheTtl = TimeSpan.FromMinutes(3);

    private static readonly ConcurrentDictionary<string, Lazy<Task<JobAggregationResult>>> _inflight
        = new();

    private static readonly Regex CollapseWhitespace = new(@"\s+", RegexOptions.Compiled);
    private static readonly Regex StripPunctuation   = new(@"[^\p{L}\p{N}\s]", RegexOptions.Compiled);

    public JobAggregationService(
        IEnumerable<IJobSourceService> sources,
        IDeduplicationService deduplication,
        IJobVacancyRepository repo,
        IMemoryCache cache,
        ILogger<JobAggregationService> logger)
    {
        _sources = sources;
        _deduplication = deduplication;
        _repo = repo;
        _cache = cache;
        _logger = logger;
    }

    public async Task<JobAggregationResult> ScrapeAndPersistAsync(
        string keywords, string? location, Country country = Country.Ukraine, CancellationToken ct = default)
    {
        var cacheKey = BuildCacheKey(keywords, location, country);

        if (_cache.TryGetValue<JobAggregationResult>(cacheKey, out var hit) && hit is not null)
        {
            _logger.LogInformation(
                "JobAggregation: cache HIT for '{Keywords}' — skipping scrape (scraped={Scraped}, " +
                "dedup-removed={Dups}, resolved={Resolved})",
                keywords, hit.ScrapedTotal, hit.DuplicatesRemoved, hit.Resolved.Count);


            return new JobAggregationResult(
                Resolved: hit.Resolved,
                NewlyInserted: Array.Empty<JobVacancy>(),
                ScrapedTotal: hit.ScrapedTotal,
                DuplicatesRemoved: hit.DuplicatesRemoved);
        }

        var lazy = _inflight.GetOrAdd(cacheKey, key => new Lazy<Task<JobAggregationResult>>(
            () => RunAndCacheAsync(key, keywords ?? string.Empty, location, country, ct),
            LazyThreadSafetyMode.ExecutionAndPublication));

        try
        {
            return await lazy.Value;
        }
        finally
        {
            _inflight.TryRemove(new KeyValuePair<string, Lazy<Task<JobAggregationResult>>>(cacheKey, lazy));
        }
    }

    private async Task<JobAggregationResult> RunAndCacheAsync(
        string cacheKey, string keywords, string? location, Country country, CancellationToken ct)
    {
        var result = await ScrapeAndPersistInternalAsync(keywords, location, country, ct);

        var ttl = result.ScrapedTotal > 0 ? ScrapeCacheTtl : EmptyResultCacheTtl;
        _cache.Set(cacheKey, result, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = ttl,
            Size = 1,
        });

        if (result.ScrapedTotal == 0)
        {
            _logger.LogInformation(
                "JobAggregation: zero-result for '{Keywords}' country={Country} cached with short TTL ({Ttl} min)",
                keywords, country, EmptyResultCacheTtl.TotalMinutes);
        }
        return result;
    }

    private static string BuildCacheKey(string? keywords, string? location, Country country)
    {
        var k = StripPunctuation.Replace((keywords ?? string.Empty).ToLowerInvariant(), " ");
        k = CollapseWhitespace.Replace(k, " ").Trim();
        var l = (location ?? string.Empty).Trim().ToLowerInvariant();
        return $"scrape:{country}:{k}:{l}";
    }

    private async Task<JobAggregationResult> ScrapeAndPersistInternalAsync(
        string keywords, string? location, Country country, CancellationToken ct)
    {


        var nonManualSources = _sources
            .Where(s => s.SourceName != "manual")
            .Where(s => country == Country.All || s.SupportedCountries.Contains(country))
            .ToList();

        _logger.LogInformation(
            "JobAggregation: country={Country} → {Count} scrapers eligible: {Sources}",
            country, nonManualSources.Count, string.Join(", ", nonManualSources.Select(s => s.SourceName)));

        var fetchTasks = nonManualSources.Select(async source =>
        {
            try
            {
                var jobs = await source.FetchJobsAsync(keywords, location, country, ct);
                _logger.LogInformation(
                    "JobAggregation: {Source} returned {Count} for '{Keywords}'",
                    source.SourceName, jobs.Count, keywords);
                return jobs;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "JobAggregation: scraper '{Source}' failed for '{Keywords}': {Message}",
                    source.SourceName, keywords, ex.Message);
                return (IReadOnlyList<JobVacancy>)Array.Empty<JobVacancy>();
            }
        });

        var perSourceResults = await Task.WhenAll(fetchTasks);
        var scraped = perSourceResults.SelectMany(r => r).ToList();
        var scrapedTotal = scraped.Count;

        if (scrapedTotal == 0)
        {
            _logger.LogInformation(
                "JobAggregation: 0 vacancies scraped across {Sources} sources for '{Keywords}'",
                nonManualSources.Count, keywords);
            return new JobAggregationResult(
                Array.Empty<JobVacancy>(),
                Array.Empty<JobVacancy>(),
                ScrapedTotal: 0,
                DuplicatesRemoved: 0);
        }


        var dedup = await _deduplication.DeduplicateAsync(scraped, ct);
        int duplicatesRemoved = dedup.Duplicates.Count;


        var dbByUrl = await _repo.GetAllByUrlAsync(ct);


        var dbByCompanyTitle = new Dictionary<string, JobVacancy>();
        foreach (var dbEntity in dbByUrl.Values)
        {
            if (dbEntity.IsDuplicate) continue;
            if (string.IsNullOrWhiteSpace(dbEntity.Company)) continue;
            var key = MakeCompanyTitleKey(dbEntity.Company, dbEntity.Title);
            if (!dbByCompanyTitle.ContainsKey(key))
                dbByCompanyTitle[key] = dbEntity;
        }


        var resolved = new List<JobVacancy>(dedup.Unique.Count);
        var newlyInserted = new List<JobVacancy>();
        int semanticDupCollapsed = 0;

        foreach (var j in dedup.Unique)
        {
            if (dbByUrl.TryGetValue(j.PrimaryUrl, out var dbEntity))
            {
                resolved.Add(dbEntity);
            }
            else if (!string.IsNullOrWhiteSpace(j.Company) &&
                     dbByCompanyTitle.TryGetValue(
                         MakeCompanyTitleKey(j.Company, j.Title), out var canonical))
            {


                resolved.Add(canonical);
                semanticDupCollapsed++;
            }
            else
            {
                resolved.Add(j);
                newlyInserted.Add(j);
            }
        }

        if (semanticDupCollapsed > 0)
        {
            _logger.LogInformation(
                "JobAggregation: '{Keywords}' → {Count} scraped entries collapsed onto " +
                "existing (Company, Title) canonicals (cross-source semantic dedup)",
                keywords, semanticDupCollapsed);
        }


        if (newlyInserted.Count > 0)
        {
            await _repo.AddRangeAsync(newlyInserted, ct);
            _logger.LogInformation(
                "JobAggregation: '{Keywords}' → scraped={Scraped}, dedup-removed={Dups}, " +
                "already-cached={Cached}, new-inserted={New}",
                keywords, scrapedTotal, duplicatesRemoved,
                resolved.Count - newlyInserted.Count, newlyInserted.Count);
        }
        else
        {
            _logger.LogInformation(
                "JobAggregation: '{Keywords}' → scraped={Scraped}, dedup-removed={Dups}, " +
                "all {Cached} already in DB",
                keywords, scrapedTotal, duplicatesRemoved, resolved.Count);
        }

        return new JobAggregationResult(
            Resolved: resolved,
            NewlyInserted: newlyInserted,
            ScrapedTotal: scrapedTotal,
            DuplicatesRemoved: duplicatesRemoved);
    }


    private static string MakeCompanyTitleKey(string? company, string? title) =>
        $"{(company ?? string.Empty).ToLower().Trim()}-{NormalizeTitle(title ?? string.Empty)}";


    private static readonly System.Text.RegularExpressions.Regex SeniorityWords =
        new(@"\b(senior|junior|middle|lead)\b",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string NormalizeTitle(string title) =>
        System.Text.RegularExpressions.Regex.Replace(
            SeniorityWords.Replace(title.ToLower(), ""),
            @"\s+",
            " ").Trim();
}
