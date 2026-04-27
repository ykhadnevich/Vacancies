using MediatR;
using Application.DTOs;
using Domain.Enums;

namespace Application.Jobs.Queries.GetAggregatedJobs;

public record GetAggregatedJobsQuery : IRequest<GetAggregatedJobsResult>
{
    public string Keywords { get; init; } = string.Empty;
    public string? Location { get; init; }
    public WorkFormat? WorkFormat { get; init; }
    public SeniorityLevel? SeniorityLevel { get; init; }
    public decimal? MinSalary { get; init; }
    public string? Category { get; init; }
    public bool RunRelevancePipeline { get; init; } = true;
}

public class GetAggregatedJobsResult
{
    public IReadOnlyList<JobVacancyDto> Jobs { get; init; } = new List<JobVacancyDto>();
    public IReadOnlyList<JobVacancyDto> Duplicates { get; init; } = new List<JobVacancyDto>();
    public int TotalCount { get; init; }
    public int DuplicatesRemoved { get; init; }
    public bool RelevancePipelineRan { get; init; }
}