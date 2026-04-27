using MediatR;
using Application.Common.Interfaces;
using Application.Common.KeywordFiltering;
using Application.DTOs;
using Application.Jobs.Queries.GetAggregatedJobs;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Vacancies.Domain.ValueObjects;

namespace Application.Jobs.Queries.GetAggregatedJobs;

public class GetAggregatedJobsHandler
    : IRequestHandler<GetAggregatedJobsQuery, GetAggregatedJobsResult>
{
    private readonly IEnumerable<IJobSourceService> _sources;
    private readonly IDeduplicationService _deduplication;
    private readonly IRelevancePipeline _relevancePipeline;
    private readonly IUserProfileRepository _userProfileRepo;
    private readonly IJobVacancyRepository _jobVacancyRepo;
    private readonly ICurrentUserService _currentUser;
    private readonly IJobDescriptionFetcher _descriptionFetcher;
    private readonly ILogger<GetAggregatedJobsHandler> _logger;

    public GetAggregatedJobsHandler(
        IEnumerable<IJobSourceService> sources,
        IDeduplicationService deduplication,
        IRelevancePipeline relevancePipeline,
        IUserProfileRepository userProfileRepo,
        IJobVacancyRepository jobVacancyRepo,
        ICurrentUserService currentUser,
        IJobDescriptionFetcher descriptionFetcher,
        ILogger<GetAggregatedJobsHandler> logger)
    {
        _sources = sources;
        _deduplication = deduplication;
        _relevancePipeline = relevancePipeline;
        _userProfileRepo = userProfileRepo;
        _jobVacancyRepo = jobVacancyRepo;
        _currentUser = currentUser;
        _descriptionFetcher = descriptionFetcher;
        _logger = logger;
    }

    public async Task<GetAggregatedJobsResult> Handle(
        GetAggregatedJobsQuery query,
        CancellationToken ct)
    {
        var fetchTasks = _sources
            .Where(s => s.SourceName != "manual")
            .Select(async source =>
            {
                try
                {
                    var jobs = await source.FetchJobsAsync(query.Keywords, query.Location, ct);
                    _logger.LogDebug("{Source}: {Count} jobs fetched", source.SourceName, jobs.Count);
                    return jobs;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Scraper error [{Source}]: {Message}", source.SourceName, ex.Message);
                    return (IReadOnlyList<JobVacancy>)new List<JobVacancy>();
                }
            });

        var results = await Task.WhenAll(fetchTasks);
        var allJobs = results.SelectMany(r => r).ToList();

        var allManualJobs = await _jobVacancyRepo.GetBySourceAsync(
            JobSource.Manual, CancellationToken.None);

        var existingUrls = allJobs.SelectMany(j => j.Urls).ToHashSet();

        var filteredManualJobs = allManualJobs
            .Where(j => !existingUrls.Contains(j.PrimaryUrl))
            .Where(j =>
            {
                if (string.IsNullOrEmpty(query.Keywords)) return true;

                var keywords = query.Keywords.ToLower()
                    .Split(' ', StringSplitOptions.RemoveEmptyEntries);

                var title = j.Title.ToLower();
                return keywords.All(k => title.Contains(k));
            })
            .GroupBy(j => j.Title.ToLower().Trim())
            .Select(g => g.First())
            .ToList();

        _logger.LogDebug("Manual jobs after filter: {Count}", filteredManualJobs.Count);

        allJobs.AddRange(filteredManualJobs);

        if (!string.IsNullOrEmpty(query.Keywords))
        {
            var keywordParts = query.Keywords
                .ToLower()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries);

            var seniorityWords = new HashSet<string>
                { "junior", "middle", "senior", "lead", "head", "intern", "trainee", "jr", "sr" };

            var genericWords = new HashSet<string>
                { "developer", "engineer", "dev", "розробник", "програміст" };

            var coreKeywords = keywordParts
                .Where(k => !seniorityWords.Contains(k) && !genericWords.Contains(k))
                .ToList();

            var seniorityKeywords = keywordParts
                .Where(k => seniorityWords.Contains(k))
                .ToList();

            allJobs = allJobs
                .Where(job =>
                {
                    if (coreKeywords.Count == 0) return true;

                    var title = job.Title.ToLower();
                    var text = job.IsManuallyAdded
                        ? $" {title} "
                        : $"{title} {job.Description ?? ""}".ToLower();
                    var urlLower = job.PrimaryUrl.ToLower();

                    return coreKeywords.All(k =>
                    {
                        var aliases = TechAliasMap.Resolve(k);

                        if (aliases.Any(alias => title.Contains(alias))) return true;
                        if (aliases.Any(alias => text.Contains(alias))) return true;

                        if (job.Source == JobSource.Djinni)
                            return aliases.Any(alias =>
                                urlLower.Contains($"-{alias.Trim()}-") ||
                                urlLower.Contains($"-{alias.Trim()}/"));

                        return false;
                    });
                })
                .OrderByDescending(job =>
                {
                    if (seniorityKeywords.Count == 0) return 1f;
                    var text = $"{job.Title} {job.Description ?? ""}".ToLower();
                    return seniorityKeywords.Any(k => text.Contains(k)) ? 2f : 1f;
                })
                .ToList();
        }

        var deduplicationResult = await _deduplication.DeduplicateAsync(allJobs, CancellationToken.None);
        var deduplicated = deduplicationResult.Unique;
        var duplicateJobs = deduplicationResult.Duplicates;
        int duplicatesRemoved = duplicateJobs.Count;

        var ranPipeline = false;
        var finalJobs = deduplicated;

        if (query.RunRelevancePipeline && _currentUser.IsAuthenticated)
        {
            var userProfile = await _userProfileRepo
                .GetByIdAsync(_currentUser.UserId!.Value, CancellationToken.None);

            if (userProfile is not null)
            {
                finalJobs = await _relevancePipeline.RunAsync(deduplicated, userProfile, CancellationToken.None);
                ranPipeline = true;
            }
        }

        var allExistingUrls = await _jobVacancyRepo.GetAllUrlsAsync(CancellationToken.None);
        var existingUrlSet = new HashSet<string>(allExistingUrls);
        var newJobs = finalJobs
            .Where(j => !existingUrlSet.Contains(j.PrimaryUrl))
            .ToList();
        if (newJobs.Any())
            await _jobVacancyRepo.AddRangeAsync(newJobs, CancellationToken.None);

        var dtos = finalJobs.Select(MapToDto).ToList();

        return new GetAggregatedJobsResult
        {
            Jobs = dtos,
            Duplicates = duplicateJobs.Select(MapToDto).ToList(),
            TotalCount = dtos.Count,
            DuplicatesRemoved = duplicatesRemoved,
            RelevancePipelineRan = ranPipeline
        };
    }

    private static JobVacancyDto MapToDto(JobVacancy job) =>
        new()
        {
            Id = job.Id,
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            Description = job.Description,
            Salary = job.Salary?.ToString(),
            PrimaryUrl = job.PrimaryUrl,
            AllUrls = job.Urls,
            Source = job.Source,
            WorkFormat = job.WorkFormat,
            SeniorityLevel = job.SeniorityLevel,
            Category = job.Category,
            RelevanceScore = job.RelevanceScore?.Value,
            RelevanceStage = job.RelevanceScore?.Stage.ToString(),
            IsDuplicate = job.IsDuplicate,
            IsManuallyAdded = job.IsManuallyAdded,
            PublishedAt = job.PublishedAt
        };
}
