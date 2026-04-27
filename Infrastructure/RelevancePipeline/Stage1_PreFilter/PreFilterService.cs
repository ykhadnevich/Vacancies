using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.ValueObjects;
using Vacancies.Domain.ValueObjects;

namespace Infrastructure.RelevancePipeline.Stage1_PreFilter;

public class PreFilterService : IPreFilterService
{
    public Task<IReadOnlyList<JobVacancy>> FilterAsync(
        IReadOnlyList<JobVacancy> jobs,
        UserProfile user,
        CancellationToken ct = default)
    {
        var filtered = jobs
            .Where(job => PassesSeniorityFilter(job, user))
            .Where(job => PassesKeywordFilter(job, user))
            .Where(job => PassesLocationFilter(job, user))
            .Where(job => PassesSalaryFilter(job, user))
            .ToList();

        return Task.FromResult<IReadOnlyList<JobVacancy>>(filtered);
    }

    private static bool PassesSeniorityFilter(JobVacancy job, UserProfile user)
    {
        if (user.SeniorityLevel == SeniorityLevel.NotSpecified) return true;
        if (job.SeniorityLevel == SeniorityLevel.NotSpecified) return true;
        return job.SeniorityLevel == user.SeniorityLevel;
    }

    private static bool PassesKeywordFilter(JobVacancy job, UserProfile user)
    {
        if (!user.Skills.Any()) return true;

        var text = $"{job.Title} {job.Description}".ToLower();

        return user.Skills.Any(skill =>
            text.Contains(skill.ToLower()));
    }

    private static bool PassesLocationFilter(JobVacancy job, UserProfile user)
    {
        if (job.WorkFormat == WorkFormat.Remote) return true;
        if (user.PreferredWorkFormat == WorkFormat.Remote) return true;
        if (string.IsNullOrEmpty(user.PreferredLocation)) return true;

        return job.Location?.ToLower()
            .Contains(user.PreferredLocation.ToLower()) ?? true;
    }

    private static bool PassesSalaryFilter(JobVacancy job, UserProfile user)
    {
        if (!user.ExpectedSalary.HasValue) return true;
        if (job.Salary?.MaxAmount is null) return true;

        return job.Salary.MaxAmount >= user.ExpectedSalary * 0.8m;
    }
}
