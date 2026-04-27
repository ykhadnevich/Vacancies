using Domain.Entities;

namespace Domain.Interfaces.Services;

public interface IRelevancePipeline
{
    Task<IReadOnlyList<JobVacancy>> RunAsync(
        IReadOnlyList<JobVacancy> jobs,
        UserProfile user,
        CancellationToken ct = default);
}
