using Microsoft.EntityFrameworkCore;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using Infrastructure.Persistence;

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
}