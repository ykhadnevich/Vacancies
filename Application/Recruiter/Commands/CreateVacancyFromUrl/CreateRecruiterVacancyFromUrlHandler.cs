using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.Recruiter.Commands.CreateVacancy;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Recruiter.Commands.CreateVacancyFromUrl;

public sealed class CreateRecruiterVacancyFromUrlHandler
    : IRequestHandler<CreateRecruiterVacancyFromUrlCommand, CreateRecruiterVacancyResult>
{
    private readonly IRecruiterVacancyScraper _scraper;
    private readonly IJobVacancyRepository _vacancies;
    private readonly IBatchedVacancyExtractionService _extractor;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreateRecruiterVacancyFromUrlHandler> _logger;

    public CreateRecruiterVacancyFromUrlHandler(
        IRecruiterVacancyScraper scraper,
        IJobVacancyRepository vacancies,
        IBatchedVacancyExtractionService extractor,
        ICurrentUserService currentUser,
        ILogger<CreateRecruiterVacancyFromUrlHandler> logger)
    {
        _scraper = scraper;
        _vacancies = vacancies;
        _extractor = extractor;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<CreateRecruiterVacancyResult> Handle(
        CreateRecruiterVacancyFromUrlCommand cmd, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");
        if (string.IsNullOrWhiteSpace(cmd.Url))
            throw new ArgumentException("URL is required.", nameof(cmd.Url));

        // 1. Targeted scrape — recruiter pastes ONE vacancy URL, we extract title /
        //    company / description for that specific page (not a listing crawl).
        var scraped = await _scraper.ScrapeAsync(cmd.Url, ct);
        if (scraped is null)
        {
            _logger.LogWarning("Recruiter URL scrape returned nothing for {Url}.", cmd.Url);
            return new CreateRecruiterVacancyResult(Guid.Empty, false,
                "Could not extract a vacancy from this URL. Try the manual form instead.");
        }

        var job = JobVacancy.Create(
            title:           scraped.Title,
            company:         scraped.Company,
            url:             cmd.Url,
            source:          JobSource.Manual,
            publishedAt:     DateTime.UtcNow,
            location:        scraped.Location,
            description:     scraped.Description,
            workFormat:      WorkFormat.NotSpecified,
            seniorityLevel:  SeniorityLevel.NotSpecified,
            isManuallyAdded: true);

        // 2. Assign owner so it surfaces only in the recruiter's cabinet.
        job.AssignOwner(userId);

        // 3. Sync normalisation — 30 s strict timeout so a Gemini hang surfaces
        //    to the recruiter instead of leaving an unanalysed row in the DB.
        bool normalised;
        string? error = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var description = job.Description ?? string.Empty;
            if (description.Trim().Length < 20)
            {
                normalised = false;
                error = "Scraped description is too short to normalise.";
            }
            else
            {
                var request = new BatchedVacancyExtractionRequest(job.Id, description);
                var result = await _extractor.ExtractBatchAsync(new[] { request }, timeoutCts.Token);

                if (result.TryGetValue(job.Id, out var extraction)
                    && !string.IsNullOrWhiteSpace(extraction.Json))
                {
                    job.SetVacancyAnalysis(extraction.Json, extraction.ModelVersion);
                    normalised = true;
                }
                else
                {
                    normalised = false;
                    error = "Empty extraction result.";
                }
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            normalised = false;
            error = "Vacancy normalisation timed out.";
            _logger.LogWarning("Vacancy normalisation timed out for {Url}.", cmd.Url);
        }
        catch (Exception ex)
        {
            normalised = false;
            error = ex.Message;
            _logger.LogWarning(ex, "Vacancy normalisation failed for {Url}.", cmd.Url);
        }

        await _vacancies.AddRangeAsync(new[] { job }, ct);

        _logger.LogInformation(
            "Recruiter {UserId} imported vacancy {VacancyId} from {Url} via {Hint}; " +
            "title='{Title}', desc={Len} chars, normalised={Normalised}.",
            userId, job.Id, cmd.Url, scraped.SourceHint,
            scraped.Title, scraped.Description.Length, normalised);

        return new CreateRecruiterVacancyResult(job.Id, normalised, error);
    }
}
