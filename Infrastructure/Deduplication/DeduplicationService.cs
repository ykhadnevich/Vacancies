using Domain.Entities;
using Domain.Interfaces.Services;

namespace Infrastructure.Deduplication;

public class DeduplicationService : IDeduplicationService
{
    public Task<DeduplicationResult> DeduplicateAsync(
        IReadOnlyList<JobVacancy> jobs,
        CancellationToken ct = default)
    {
        var unique = new List<JobVacancy>();
        var duplicates = new List<JobVacancy>();
        var seen = new Dictionary<string, JobVacancy>();

        foreach (var job in jobs)
        {
            var key = $"{job.Company.ToLower().Trim()}-{NormalizeTitle(job.Title)}";

            if (seen.TryGetValue(key, out var existing))
            {
                foreach (var url in job.Urls)
                    existing.AddDuplicateUrl(url);

                job.MarkAsDuplicate(existing.Id);
                duplicates.Add(job);
            }
            else
            {
                seen[key] = job;
                unique.Add(job);
            }
        }

        return Task.FromResult(new DeduplicationResult(unique, duplicates));
    }

    private static string NormalizeTitle(string title) =>
        title.ToLower()
            .Replace("senior", "")
            .Replace("junior", "")
            .Replace("middle", "")
            .Replace("lead", "")
            .Trim();
}