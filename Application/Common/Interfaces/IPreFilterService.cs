using Domain.Entities;

namespace Application.Common.Interfaces;

public interface IPreFilterService
{
    Task<IReadOnlyList<JobVacancy>> FilterAsync(
        IReadOnlyList<JobVacancy> jobs,
        UserProfile user,
        CancellationToken ct = default);
}
