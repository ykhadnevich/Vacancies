using MediatR;
using Application.DTOs;
using Domain.Enums;

namespace Application.Tracker.Queries.GetUserApplications;

public record GetUserApplicationsQuery : IRequest<IReadOnlyList<ApplicationTrackerDto>>
{
    public ApplicationStatus? FilterByStatus { get; init; }
}
