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


    private static readonly System.Text.RegularExpressions.Regex SeniorityWords =
        new(@"\b(senior|junior|middle|lead)\b",
            System.Text.RegularExpressions.RegexOptions.Compiled |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);

    private static string NormalizeTitle(string title) =>
        System.Text.RegularExpressions.Regex.Replace(
            SeniorityWords.Replace(title.ToLower(), ""),
            @"\s+",
            " ").Trim();
}
