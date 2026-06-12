using MediatR;
using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Domain.Interfaces.Repositories;

namespace Application.Tracker.Commands.AddToTracker;

public class AddToTrackerHandler : IRequestHandler<AddToTrackerCommand, ApplicationTrackerDto>
{
    private readonly IApplicationRepository  _appRepo;
    private readonly IJobVacancyRepository   _jobRepo;
    private readonly IUserProfileRepository  _userRepo;
    private readonly ICurrentUserService     _currentUser;

    public AddToTrackerHandler(
        IApplicationRepository appRepo,
        IJobVacancyRepository jobRepo,
        IUserProfileRepository userRepo,
        ICurrentUserService currentUser)
    {
        _appRepo     = appRepo;
        _jobRepo     = jobRepo;
        _userRepo    = userRepo;
        _currentUser = currentUser;
    }

    public async Task<ApplicationTrackerDto> Handle(
        AddToTrackerCommand command,
        CancellationToken ct)
    {
        if (!_currentUser.IsAuthenticated)
            throw new UnauthorizedAccessException();

        var userId = _currentUser.UserId!.Value;

        ApplicationTracker application;

        if (command.JobVacancyId.HasValue)
        {
            var job = await _jobRepo.GetByIdAsync(command.JobVacancyId.Value, ct)
                ?? throw new Exception("Job not found");

            application = ApplicationTracker.CreateFromFeed(userId, job);
        }
        else
        {
            application = ApplicationTracker.CreateManually(
                userId,
                command.Title!,
                command.Company!,
                command.Url ?? string.Empty,
                command.Location,
                command.Salary,
                command.SeniorityLevel);
        }


        if (command.Score.HasValue && !string.IsNullOrWhiteSpace(command.Verdict))
        {
            string? cvFileName = null;
            try
            {
                var profile = await _userRepo.GetByIdAsync(userId, ct);
                cvFileName  = profile?.CvFileUrl;
            }
            catch
            {

            }

            application.AttachAnalysis(
                command.Score.Value,
                command.Verdict!,
                command.MatchedSkills      ?? new List<string>(),
                command.MissingMustHaves   ?? new List<string>(),
                command.TriggeredAntiFlags ?? new List<string>(),
                command.ReasonShort,
                command.StrengthsEn,
                command.StrengthsUk,
                command.GapsEn,
                command.GapsUk,
                command.RecommendationEn,
                command.RecommendationUk,
                command.SubScores,
                cvFileName,
                command.PipelineVersion ?? "unknown");
        }

        await _appRepo.AddAsync(application, ct);
        return MapToDto(application);
    }

    private static ApplicationTrackerDto MapToDto(ApplicationTracker a) =>
        new()
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
        };
}
