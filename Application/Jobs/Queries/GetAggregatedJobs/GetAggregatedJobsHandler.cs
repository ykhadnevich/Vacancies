using MediatR;
using Application.Common.Enums;
using Application.Common.Exceptions;
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
    private readonly IReasoningContext _reasoningContext;
    private readonly ILogger<GetAggregatedJobsHandler> _logger;

    public GetAggregatedJobsHandler(
        IEnumerable<IJobSourceService> sources,
        IDeduplicationService deduplication,
        IRelevancePipeline relevancePipeline,
        IUserProfileRepository userProfileRepo,
        IJobVacancyRepository jobVacancyRepo,
        ICurrentUserService currentUser,
        IJobDescriptionFetcher descriptionFetcher,
        IReasoningContext reasoningContext,
        ILogger<GetAggregatedJobsHandler> logger)
    {
        _sources = sources;
        _deduplication = deduplication;
        _relevancePipeline = relevancePipeline;
        _userProfileRepo = userProfileRepo;
        _jobVacancyRepo = jobVacancyRepo;
        _currentUser = currentUser;
        _descriptionFetcher = descriptionFetcher;
        _reasoningContext = reasoningContext;
        _logger = logger;
    }

    public async Task<GetAggregatedJobsResult> Handle(
        GetAggregatedJobsQuery query,
        CancellationToken ct)
    {
        var fetchTasks = _sources
            .Where(s => s.SourceName != "manual")
            .Where(s => query.Country == Country.All || s.SupportedCountries.Contains(query.Country))
            .Select(async source =>
            {
                try
                {
                    var jobs = await source.FetchJobsAsync(query.Keywords, query.Location, query.Country, ct);
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


        var dbJobsByUrl = await _jobVacancyRepo.GetAllByUrlAsync(CancellationToken.None);
        var existingUrlSet = new HashSet<string>(dbJobsByUrl.Keys);


        var resolvedJobs = deduplicated
            .Select(j =>
            {
                if (!dbJobsByUrl.TryGetValue(j.PrimaryUrl, out var dbJob))
                    return j;


                dbJob.SetCompanySignals(j.ApplicantCount, j.RecruiterRespondsQuickly);
                return dbJob;
            })
            .ToList();


        _reasoningContext.Provider = query.ReasoningProvider;
        _reasoningContext.ScoringModel = query.ScoringModel;
        _reasoningContext.CvVersion = query.CvVersion;
        _reasoningContext.IncludeCompetitionSignals = query.IncludeCompetitionSignals;
        _reasoningContext.IncludeRecencyDecay = query.IncludeRecencyDecay;

        var runPipeline = query.ReasoningProvider != ReasoningProviderType.None;
        var ranPipeline = false;
        var finalJobs = resolvedJobs;

        if (runPipeline && _currentUser.IsAuthenticated)
        {
            var userProfile = await _userProfileRepo
                .GetByIdAsync(_currentUser.UserId!.Value, CancellationToken.None);

            if (userProfile is not null)
            {


                if (query.CvVersion == CvVersionPreference.Structured
                    && string.IsNullOrWhiteSpace(userProfile.CvSummary))
                {
                    throw new CvNotReadyException();
                }

                finalJobs = (await _relevancePipeline.RunAsync(resolvedJobs, userProfile, CancellationToken.None)).ToList();
                ranPipeline = true;
            }
        }


        var signalUpdates = deduplicated
            .Where(j => existingUrlSet.Contains(j.PrimaryUrl)
                     && (j.ApplicantCount.HasValue || j.RecruiterRespondsQuickly.HasValue))
            .Select(j => (j.PrimaryUrl, j.ApplicantCount, j.RecruiterRespondsQuickly))
            .ToList();
        if (signalUpdates.Any())
            await _jobVacancyRepo.UpdateCompanySignalsAsync(signalUpdates, CancellationToken.None);


        var newJobs = finalJobs
            .Where(j => !existingUrlSet.Contains(j.PrimaryUrl))
            .ToList();
        if (newJobs.Any())
            await _jobVacancyRepo.AddRangeAsync(newJobs, CancellationToken.None);


        if (ranPipeline)
        {
            var existingWithScores = finalJobs
                .Where(j => existingUrlSet.Contains(j.PrimaryUrl) && j.RelevanceScore != null)
                .Select(j => (j.PrimaryUrl, j.RelevanceScore!.Value, j.RelevanceScore.Stage))
                .ToList();

            if (existingWithScores.Any())
                await _jobVacancyRepo.UpdateRelevanceScoresAsync(existingWithScores, CancellationToken.None);
        }


        var sortedJobs = finalJobs
            .OrderByDescending(j => j.RelevanceScore?.Value ?? -1f)
            .ToList();

        var dtos = sortedJobs.Select(MapToDto).ToList();

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
            RelevanceReason = job.Reason,
            IsDuplicate = job.IsDuplicate,
            IsManuallyAdded = job.IsManuallyAdded,
            PublishedAt = job.PublishedAt,
            ApplicantCount = job.ApplicantCount,
            RecruiterRespondsQuickly = job.RecruiterRespondsQuickly
        };
}
