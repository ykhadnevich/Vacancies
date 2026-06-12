using Domain.Entities;

namespace Application.Common.Interfaces;


public interface IJobAggregationService
{
    Task<JobAggregationResult> ScrapeAndPersistAsync(
        string keywords,
        string? location,
        CancellationToken ct = default);
}


public sealed record JobAggregationResult(
    System.Collections.Generic.IReadOnlyList<Domain.Entities.JobVacancy> Resolved,
    System.Collections.Generic.IReadOnlyList<Domain.Entities.JobVacancy> NewlyInserted,
    int ScrapedTotal,
    int DuplicatesRemoved);
