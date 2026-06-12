using MediatR;
using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Interfaces.Repositories;

namespace Application.Tracker.Queries.GetUserApplications;

public class GetUserApplicationsHandler
    : IRequestHandler<GetUserApplicationsQuery, IReadOnlyList<ApplicationTrackerDto>>
{
    private readonly IApplicationRepository _repo;
    private readonly ICurrentUserService _currentUser;

    public GetUserApplicationsHandler(
        IApplicationRepository repo,
        ICurrentUserService currentUser)
    {
        _repo = repo;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<ApplicationTrackerDto>> Handle(
        GetUserApplicationsQuery query,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        var userId = _currentUser.UserId!.Value;

        var applications = await _repo.GetByUserIdAsync(userId, ct);

        if (query.FilterByStatus.HasValue)
            applications = applications
                .Where(a => a.Status == query.FilterByStatus.Value)
                .ToList();

        return applications.Select(a => new ApplicationTrackerDto
        {
            Id                 = a.Id,
            JobVacancyId       = a.JobVacancyId,
            Title              = a.Title,
            Company            = a.Company,
            Location           = a.Location,
            Salary             = a.Salary,
            Url                = a.Url,
            SeniorityLevel     = a.SeniorityLevel,
            Status             = a.Status,
            PipelineSteps      = a.PipelineSteps,
            Notes              = a.Notes,
            AddedAt            = a.AddedAt,
            UpdatedAt          = a.UpdatedAt,
            IsManuallyAdded    = a.IsManuallyAdded,
            Score              = a.Score,
            Verdict            = a.Verdict,
            MatchedSkills      = a.MatchedSkills,
            MissingMustHaves   = a.MissingMustHaves,
            TriggeredAntiFlags = a.TriggeredAntiFlags,
            ReasonShort        = a.ReasonShort,
            StrengthsEn        = a.StrengthsEn,
            StrengthsUk        = a.StrengthsUk,
            GapsEn             = a.GapsEn,
            GapsUk             = a.GapsUk,
            RecommendationEn   = a.RecommendationEn,
            RecommendationUk   = a.RecommendationUk,
            SubScores          = a.SubScores,
            CvFileName         = a.CvFileName,
            PipelineVersion    = a.PipelineVersion,
            AnalyzedAt         = a.AnalyzedAt,
        }).ToList();
    }
}
