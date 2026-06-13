using Domain.Entities;
using Domain.Enums;

namespace Domain.Interfaces.Services;

public interface IJobSourceService
{
    string SourceName { get; }

    IReadOnlyList<Country> SupportedCountries { get; }

    Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        Country country = Country.Ukraine,
        CancellationToken ct = default);
}
