using Domain.Entities;
using Domain.Enums;

namespace Domain.Interfaces.Repositories;

public interface IJobVacancyRepository
{
    Task<JobVacancy?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<JobVacancy>> GetAllAsync(CancellationToken ct = default);
    Task<IReadOnlyList<JobVacancy>> GetBySourceAsync(JobSource source, CancellationToken ct = default);
    Task AddRangeAsync(IEnumerable<JobVacancy> jobs, CancellationToken ct = default);
    Task UpdateAsync(JobVacancy job, CancellationToken ct = default);
    Task<bool> ExistsByUrlAsync(string url, CancellationToken ct = default);
    Task DeleteBySourceUrlAsync(string sourceUrl, CancellationToken ct = default);
    Task<IReadOnlyList<string>> GetAllUrlsAsync(CancellationToken ct = default);
}