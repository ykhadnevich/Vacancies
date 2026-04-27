using Domain.Entities;

namespace Domain.Interfaces.Services;

public record DeduplicationResult(
    IReadOnlyList<JobVacancy> Unique,
    IReadOnlyList<JobVacancy> Duplicates);

public interface IDeduplicationService
{
    Task<DeduplicationResult> DeduplicateAsync(
        IReadOnlyList<JobVacancy> jobs,
        CancellationToken ct = default);
}