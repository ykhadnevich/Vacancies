using Domain.Entities;

namespace Domain.Interfaces.Services;

public interface IJobSourceService
{
    string SourceName { get; }

    Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        CancellationToken ct = default);
}
