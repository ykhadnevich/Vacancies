using MediatR;
using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Entities;
using Domain.Interfaces.Repositories;

namespace Application.Tracker.Commands.AddToTracker;

public class AddToTrackerHandler : IRequestHandler<AddToTrackerCommand, ApplicationTrackerDto>
{
    private readonly IApplicationRepository _appRepo;
    private readonly IJobVacancyRepository _jobRepo;
    private readonly ICurrentUserService _currentUser;

    public AddToTrackerHandler(
        IApplicationRepository appRepo,
        IJobVacancyRepository jobRepo,
        ICurrentUserService currentUser)
    {
        _appRepo = appRepo;
        _jobRepo = jobRepo;
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
                command.Salary,
                command.SeniorityLevel);
        }

        await _appRepo.AddAsync(application, ct);
        return MapToDto(application);
    }

    private static ApplicationTrackerDto MapToDto(ApplicationTracker a) =>
        new()
        {
            Id = a.Id,
            JobVacancyId = a.JobVacancyId,
            Title = a.Title,
            Company = a.Company,
            Salary = a.Salary,
            Url = a.Url,
            SeniorityLevel = a.SeniorityLevel,
            Status = a.Status,
            PipelineSteps = a.PipelineSteps,
            Notes = a.Notes,
            AddedAt = a.AddedAt,
            UpdatedAt = a.UpdatedAt,
            IsManuallyAdded = a.IsManuallyAdded
        };
}