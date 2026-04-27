using MediatR;
using Application.DTOs;
using Domain.Enums;

namespace Application.Tracker.Commands.AddToTracker;

public record AddToTrackerCommand : IRequest<ApplicationTrackerDto>
{
    public Guid? JobVacancyId { get; init; }
    public string? Title { get; init; }
    public string? Company { get; init; }
    public string? Url { get; init; }
    public string? Salary { get; init; }
    public SeniorityLevel SeniorityLevel { get; init; } = SeniorityLevel.NotSpecified;
}
