using MediatR;
using Application.DTOs;
using Domain.Enums;

namespace Application.Jobs.Queries.GetAggregatedJobsV6;


public sealed record GetAggregatedJobsV6Query : IRequest<GetAggregatedJobsV6Result>
{
    public string Keywords { get; init; } = string.Empty;
    public string? Location { get; init; }
    public Country Country { get; init; } = Country.Ukraine;
    public WorkFormat? WorkFormat { get; init; }
    public SeniorityLevel? SeniorityLevel { get; init; }
    public decimal? MinSalary { get; init; }
    public string? Category { get; init; }


    public int Limit { get; init; } = 50;
}
