using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;
using Vacancies.Domain.ValueObjects;

namespace Infrastructure.Persistence.Repositories;

public class JobVacancyRepository : IJobVacancyRepository
{
    private readonly AppDbContext _context;

    public JobVacancyRepository(AppDbContext context) => _context = context;

    public async Task<JobVacancy?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _context.JobVacancies.FindAsync(new object[] { id }, ct);

    public async Task<IReadOnlyList<JobVacancy>> GetAllAsync(CancellationToken ct = default)
        => await _context.JobVacancies
            .OrderByDescending(j => j.PublishedAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<JobVacancy>> GetBySourceAsync(
        JobSource source, CancellationToken ct = default)
        => await _context.JobVacancies
            .Where(j => j.Source == source)
            .ToListAsync(ct);

    public async Task AddRangeAsync(
        IEnumerable<JobVacancy> jobs, CancellationToken ct = default)
    {
        await _context.JobVacancies.AddRangeAsync(jobs, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(JobVacancy job, CancellationToken ct = default)
    {
        _context.JobVacancies.Update(job);
        await _context.SaveChangesAsync(ct);
    }

    public async Task<bool> ExistsByUrlAsync(string url, CancellationToken ct = default)
    {
        var allUrls = await _context.JobVacancies
            .AsNoTracking()
            .Select(j => j.Urls)
            .ToListAsync(ct);

        return allUrls.Any(urls => urls.Contains(url));
    }

    public async Task DeleteBySourceUrlAsync(string sourceUrl, CancellationToken ct = default)
    {
        var manualJobs = await _context.JobVacancies
            .Where(j => j.IsManuallyAdded)
            .ToListAsync(ct);

        var toDelete = manualJobs
            .Where(j => j.Urls.Contains(sourceUrl))
            .ToList();

        if (toDelete.Any())
        {
            _context.JobVacancies.RemoveRange(toDelete);
            await _context.SaveChangesAsync(ct);
        }
    }

    public async Task<IReadOnlyList<string>> GetAllUrlsAsync(CancellationToken ct = default)
    {
        var allUrls = await _context.JobVacancies
            .Select(j => j.Urls)
            .ToListAsync(ct);

        return allUrls.SelectMany(u => u).ToList();
    }


    public async Task<IReadOnlyDictionary<string, JobVacancy>> GetAllByUrlAsync(
        CancellationToken ct = default)
    {
        var jobs = await _context.JobVacancies
            .AsNoTracking()
            .ToListAsync(ct);

        return jobs
            .GroupBy(j => j.PrimaryUrl)
            .ToDictionary(g => g.Key, g => g.First());
    }


    public async Task<IReadOnlyList<JobVacancy>> GetJobsWithoutEmbeddingAsync(
        int batch, CancellationToken ct = default)
        => await _context.JobVacancies
            .Where(j => j.Embedding == null && j.Description != null)
            .OrderByDescending(j => j.PublishedAt)
            .Take(batch)
            .ToListAsync(ct);


    public async Task SaveEmbeddingsAsync(
        IReadOnlyList<JobVacancy> jobs, CancellationToken ct = default)
    {
        foreach (var job in jobs)
            _context.JobVacancies.Update(job);

        await _context.SaveChangesAsync(ct);
    }


    public async Task UpdateRelevanceScoresAsync(
        IReadOnlyList<(string PrimaryUrl, float Score, ScoringStage Stage)> updates,
        CancellationToken ct = default)
    {
        foreach (var (url, score, stage) in updates)
        {
            var stageInt = (int)stage;
            await _context.Database.ExecuteSqlInterpolatedAsync(
                $"""
                UPDATE "JobVacancies"
                SET "RelevanceScore" = {score}, "RelevanceStage" = {stageInt}
                WHERE "Urls" LIKE {"%" + url + "%"}
                """, ct);
        }
    }

    // P5: persists applicant count and recruiter response speed for existing jobs.
    public async Task UpdateCompanySignalsAsync(
        IReadOnlyList<(string PrimaryUrl, int? ApplicantCount, bool? RespondsQuickly)> updates,
        CancellationToken ct = default)
    {
        foreach (var (url, applicantCount, respondsQuickly) in updates)
        {
            // Only update fields that are not null — don't overwrite existing data with null
            if (applicantCount is null && respondsQuickly is null) continue;

            if (applicantCount.HasValue && respondsQuickly.HasValue)
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "JobVacancies" SET "ApplicantCount" = {applicantCount}, "RecruiterRespondsQuickly" = {respondsQuickly} WHERE "Urls" LIKE {"%" + url + "%"}""", ct);
            else if (applicantCount.HasValue)
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "JobVacancies" SET "ApplicantCount" = {applicantCount} WHERE "Urls" LIKE {"%" + url + "%"}""", ct);
            else
                await _context.Database.ExecuteSqlInterpolatedAsync(
                    $"""UPDATE "JobVacancies" SET "RecruiterRespondsQuickly" = {respondsQuickly} WHERE "Urls" LIKE {"%" + url + "%"}""", ct);
        }
    }

    // v6 production wiring — VacancyAnalysisWorker integration.
    public async Task<IReadOnlyList<JobVacancy>> GetJobsWithoutAnalysisAsync(
        int batch, CancellationToken ct = default)
        => await _context.JobVacancies
            .Where(j => j.VacancyAnalysisJson == null && j.Description != null)
            .OrderByDescending(j => j.PublishedAt)
            .Take(batch)
            .ToListAsync(ct);

    public async Task SaveVacancyAnalysisAsync(
        Guid vacancyId, string analysisJson, string modelVersion, CancellationToken ct = default)
    {
        // Targeted UPDATE — only touches the 3 analysis columns to avoid
        // change-tracking the whole entity. Faster + lower lock contention.
        var now = DateTime.UtcNow;
        await _context.Database.ExecuteSqlInterpolatedAsync(
            $"""
            UPDATE "JobVacancies"
            SET "VacancyAnalysisJson" = {analysisJson},
                "VacancyAnalysisModelVersion" = {modelVersion},
                "VacancyAnalyzedAt" = {now}
            WHERE "Id" = {vacancyId}
            """, ct);
    }

    public async Task<IReadOnlyList<JobVacancy>> ListByOwnerAsync(
        Guid ownerUserId, CancellationToken ct = default)
        => await _context.JobVacancies
            .AsNoTracking()
            .Where(j => j.OwnerUserId == ownerUserId)
            .OrderByDescending(j => j.PublishedAt)
            .ToListAsync(ct);
}
