using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Domain.Scoring;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Recruiter.Commands.AddCandidatesToList;

public sealed class AddCandidatesToListHandler
    : IRequestHandler<AddCandidatesToListCommand, AddCandidatesToListResult>
{
    private readonly IRecruiterCandidateRepository _candidates;
    private readonly ICvExtractionService _cvExtractor;
    private readonly ICurrentUserService _currentUser;
    private readonly ILogger<AddCandidatesToListHandler> _logger;

    // Caps simultaneous Gemini calls per request — Gemini free tier is ~10 RPM,
    // so 4 parallel keeps us safely below the rate limit even with a busy cabinet.
    private const int NormalizeConcurrency = 4;

    public AddCandidatesToListHandler(
        IRecruiterCandidateRepository candidates,
        ICvExtractionService cvExtractor,
        ICurrentUserService currentUser,
        ILogger<AddCandidatesToListHandler> logger)
    {
        _candidates = candidates;
        _cvExtractor = cvExtractor;
        _currentUser = currentUser;
        _logger = logger;
    }

    public async Task<AddCandidatesToListResult> Handle(
        AddCandidatesToListCommand cmd, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        if (cmd.Candidates is null || cmd.Candidates.Count == 0)
            return new AddCandidatesToListResult(0, 0, Array.Empty<AddedCandidateSummary>());

        using var gate = new SemaphoreSlim(NormalizeConcurrency, NormalizeConcurrency);
        var summaries = new List<AddedCandidateSummary>(cmd.Candidates.Count);

        var tasks = cmd.Candidates.Select(async input =>
        {
            await gate.WaitAsync(ct);
            try
            {
                return await NormalizeOneAsync(userId, input, ct);
            }
            finally
            {
                gate.Release();
            }
        }).ToList();

        var results = await Task.WhenAll(tasks);

        // Persist after all normalization completes so a Gemini hiccup on one CV
        // does not roll back the others.
        foreach (var (candidate, error) in results)
        {
            if (candidate is null) continue;

            await _candidates.AddAsync(candidate, ct);
            await _candidates.AddToListAsync(cmd.CandidateListId, candidate.Id, ct);

            summaries.Add(new AddedCandidateSummary(
                candidate.Id,
                candidate.CandidateName,
                candidate.Status == Domain.Enums.CandidateNormalizationStatus.Normalized,
                error));
        }

        var normalized = summaries.Count(s => s.Normalized);
        var failed = summaries.Count - normalized;

        _logger.LogInformation(
            "Added {Total} candidates to list {ListId} (normalised={Normalized}, failed={Failed}).",
            summaries.Count, cmd.CandidateListId, normalized, failed);

        return new AddCandidatesToListResult(normalized, failed, summaries);
    }

    private async Task<(RecruiterCandidate? candidate, string? error)> NormalizeOneAsync(
        Guid recruiterUserId,
        NewCandidateInput input,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.CvRawText))
            return (null, "empty CV text");

        var candidate = RecruiterCandidate.Create(recruiterUserId, input.CvRawText, input.CandidateName);

        try
        {
            var extracted = await _cvExtractor.ExtractAsync(input.CvRawText, ct);
            if (string.IsNullOrWhiteSpace(extracted.Summary) || string.IsNullOrWhiteSpace(extracted.ModelVersion))
            {
                candidate.MarkFailed("Empty normalisation result.");
                return (candidate, "empty normalisation result");
            }

            var hash = CvHasher.ComputeHash(extracted.Summary);
            candidate.MarkNormalized(extracted.Summary, hash, extracted.ModelVersion);
            return (candidate, null);
        }
        catch (Exception ex)
        {
            candidate.MarkFailed(ex.Message);
            _logger.LogWarning(ex, "CV normalisation failed for recruiter {UserId}.", recruiterUserId);
            return (candidate, ex.Message);
        }
    }
}
