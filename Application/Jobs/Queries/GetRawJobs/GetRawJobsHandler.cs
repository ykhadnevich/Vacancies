using Application.Common.Interfaces;
using Application.DTOs;
using Domain.Entities;
using MediatR;

namespace Application.Jobs.Queries.GetRawJobs;


public sealed class GetRawJobsHandler
    : IRequestHandler<GetRawJobsQuery, GetRawJobsResult>
{
    private readonly IJobAggregationService _aggregator;

    public GetRawJobsHandler(IJobAggregationService aggregator)
    {
        _aggregator = aggregator;
    }

    public async Task<GetRawJobsResult> Handle(GetRawJobsQuery request, CancellationToken ct)
    {
        var keywords = (request.Keywords ?? string.Empty).Trim();

        var aggregation = await _aggregator.ScrapeAndPersistAsync(keywords, request.Location, ct);

        var top = aggregation.Resolved
            .OrderByDescending(j => j.PublishedAt)
            .Take(Math.Max(1, request.Limit))
            .Select(ToDto)
            .ToList();

        return new GetRawJobsResult
        {
            Jobs              = top,
            TotalCount        = aggregation.Resolved.Count,
            DuplicatesRemoved = aggregation.DuplicatesRemoved,
        };
    }

    private static JobVacancyDto ToDto(JobVacancy j) => new()
    {
        Id                       = j.Id,
        Title                    = j.Title,
        Company                  = j.Company,
        Location                 = j.Location,
        Description              = j.Description,
        Salary                   = j.Salary?.ToString(),
        PrimaryUrl               = j.PrimaryUrl,
        AllUrls                  = j.Urls.ToList(),
        Source                   = j.Source,
        WorkFormat               = j.WorkFormat,
        SeniorityLevel           = j.SeniorityLevel,
        Category                 = j.Category,
        IsDuplicate              = j.IsDuplicate,
        IsManuallyAdded          = j.IsManuallyAdded,
        PublishedAt              = j.PublishedAt,
        ApplicantCount           = j.ApplicantCount,
        RecruiterRespondsQuickly = j.RecruiterRespondsQuickly,
    };
}
