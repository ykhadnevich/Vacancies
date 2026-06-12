using MediatR;
using Application.Common.Enums;
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


    public ReasoningProviderType ReasoningProvider { get; init; } = ReasoningProviderType.None;


    public ScoringModelType ScoringModel { get; init; } = ScoringModelType.Flash;


    public CvVersionPreference CvVersion { get; init; } = CvVersionPreference.Auto;


    public bool IncludeCompetitionSignals { get; init; } = false;


    public bool IncludeRecencyDecay { get; init; } = false;
}

public class GetAggregatedJobsResult
{
    public IReadOnlyList<JobVacancyDto> Jobs { get; init; } = new List<JobVacancyDto>();
    public IReadOnlyList<JobVacancyDto> Duplicates { get; init; } = new List<JobVacancyDto>();
    public int TotalCount { get; init; }
    public int DuplicatesRemoved { get; init; }
    public bool RelevancePipelineRan { get; init; }
}
