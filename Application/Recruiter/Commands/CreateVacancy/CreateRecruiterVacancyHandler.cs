using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Recruiter.Commands.CreateVacancy;

public sealed class CreateRecruiterVacancyHandler
    : IRequestHandler<CreateRecruiterVacancyCommand, CreateRecruiterVacancyResult>
{
    private readonly IJobVacancyRepository _vacancies;
    private readonly IBatchedVacancyExtractionService _extractor;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<CreateRecruiterVacancyHandler> _logger;

    public CreateRecruiterVacancyHandler(
        IJobVacancyRepository vacancies,
        IBatchedVacancyExtractionService extractor,
        ICurrentUserService currentUser,
        ILogger<CreateRecruiterVacancyHandler> logger)
    {
        _vacancies = vacancies;
        _extractor = extractor;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<CreateRecruiterVacancyResult> Handle(
        CreateRecruiterVacancyCommand cmd, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        if (string.IsNullOrWhiteSpace(cmd.Title))
            throw new ArgumentException("Title is required.", nameof(cmd.Title));
        if (string.IsNullOrWhiteSpace(cmd.RawDescription) || cmd.RawDescription.Trim().Length < 20)
            throw new ArgumentException("Description is too short to score against.", nameof(cmd.RawDescription));

        // Synthetic URL keeps the existing JobVacancy.Urls invariant satisfied
        // without colliding with any real scraped URL.
        var syntheticUrl = $"recruiter://{userId}/{Guid.NewGuid()}";

        var vacancy = JobVacancy.Create(
            title:          cmd.Title.Trim(),
            company:        string.IsNullOrWhiteSpace(cmd.Company) ? "—" : cmd.Company.Trim(),
            url:            syntheticUrl,
            source:         JobSource.Manual,
            publishedAt:    DateTime.UtcNow,
            location:       cmd.Location,
            description:    cmd.RawDescription,
            workFormat:     WorkFormat.NotSpecified,
            seniorityLevel: SeniorityLevel.NotSpecified,
            isManuallyAdded: true);

        vacancy.AssignOwner(userId);

        // Sync vacancy normalisation. Strict 30-second timeout: if Gemini hangs we
        // surface the failure to the recruiter rather than persist an incomplete row.
        bool normalised;
        string? normalisationError = null;
        try
        {
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(30));

            var request = new BatchedVacancyExtractionRequest(vacancy.Id, cmd.RawDescription);
            var result = await _extractor.ExtractBatchAsync(new[] { request }, timeoutCts.Token);

            if (result.TryGetValue(vacancy.Id, out var extraction)
                && !string.IsNullOrWhiteSpace(extraction.Json))
            {
                vacancy.SetVacancyAnalysis(extraction.Json, extraction.ModelVersion);
                normalised = true;
            }
            else
            {
                normalised = false;
                normalisationError = "Empty extraction result.";
            }
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            normalised = false;
            normalisationError = "Vacancy normalisation timed out.";
            _logger.LogWarning("Vacancy normalisation timed out for recruiter {UserId}.", userId);
        }
        catch (Exception ex)
        {
            normalised = false;
            normalisationError = ex.Message;
            _logger.LogWarning(ex, "Vacancy normalisation failed for recruiter {UserId}.", userId);
        }

        await _vacancies.AddRangeAsync(new[] { vacancy }, ct);

        _logger.LogInformation(
            "Created recruiter vacancy {VacancyId} for owner {OwnerId}; normalised={Normalised}.",
            vacancy.Id, userId, normalised);

        return new CreateRecruiterVacancyResult(vacancy.Id, normalised, normalisationError);
    }
}
