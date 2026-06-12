using MediatR;
using Application.DTOs;

namespace Application.Jobs.Queries.GetRawJobs;


public sealed record GetRawJobsQuery : IRequest<GetRawJobsResult>
{
    public string  Keywords { get; init; } = string.Empty;
    public string? Location { get; init; }
    public int     Limit    { get; init; } = 100;
}


public sealed record GetRawJobsResult
{
    public required IReadOnlyList<JobVacancyDto> Jobs              { get; init; }
    public required int                          TotalCount        { get; init; }
    public required int                          DuplicatesRemoved { get; init; }
}
