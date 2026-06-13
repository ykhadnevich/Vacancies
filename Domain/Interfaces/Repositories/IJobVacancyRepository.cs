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


    Task<IReadOnlyDictionary<string, JobVacancy>> GetAllByUrlAsync(CancellationToken ct = default);


    Task<IReadOnlyList<JobVacancy>> GetJobsWithoutEmbeddingAsync(int batch, CancellationToken ct = default);
    Task SaveEmbeddingsAsync(IReadOnlyList<JobVacancy> jobs, CancellationToken ct = default);


    Task UpdateRelevanceScoresAsync(
        IReadOnlyList<(string PrimaryUrl, float Score, ScoringStage Stage)> updates,
        CancellationToken ct = default);


    Task UpdateCompanySignalsAsync(
        IReadOnlyList<(string PrimaryUrl, int? ApplicantCount, bool? RespondsQuickly)> updates,
        CancellationToken ct = default);


    Task<IReadOnlyList<JobVacancy>> GetJobsWithoutAnalysisAsync(int batch, CancellationToken ct = default);


    Task SaveVacancyAnalysisAsync(
        Guid vacancyId, string analysisJson, string modelVersion, CancellationToken ct = default);


    Task<IReadOnlyList<JobVacancy>> GetJobsWithEmptyDescriptionAsync(
        int batch, int maxAgeDays, CancellationToken ct = default);


    /// <summary>Vacancies created by the given recruiter through the recruiter cabinet.</summary>
    Task<IReadOnlyList<JobVacancy>> ListByOwnerAsync(Guid ownerUserId, CancellationToken ct = default);
}