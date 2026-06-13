using MediatR;
using Application.DTOs;
using Domain.Enums;

namespace Application.Jobs.Queries.GetRawJobs;


public sealed record GetRawJobsQuery : IRequest<GetRawJobsResult>
{
    public string  Keywords { get; init; } = string.Empty;
    public string? Location { get; init; }
    public Country Country  { get; init; } = Country.Ukraine;
    public int     Limit    { get; init; } = 100;
}


public sealed record GetRawJobsResult
{
    public required IReadOnlyList<JobVacancyDto> Jobs              { get; init; }
    public required int                          TotalCount        { get; init; }
    public required int                          DuplicatesRemoved { get; init; }
}
