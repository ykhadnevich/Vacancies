using System.Text.Json;
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
            .Where(job => PassesDescriptionFilter(job))
            .Where(job => PassesSeniorityFilter(job, user))
            .Where(job => PassesKeywordFilter(job, user))
            .Where(job => PassesLocationFilter(job, user))
            .Where(job => PassesSalaryFilter(job, user))
            .Where(job => PassesRoleMismatchFilter(job, user))
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


    private static bool PassesDescriptionFilter(JobVacancy job)
    {
        if (job.IsManuallyAdded) return true;
        return !string.IsNullOrWhiteSpace(job.Description)
               && job.Description.Length >= 80;
    }

    private static bool PassesSalaryFilter(JobVacancy job, UserProfile user)
    {
        if (!user.ExpectedSalary.HasValue) return true;
        if (job.Salary?.MaxAmount is null) return true;

        return job.Salary.MaxAmount >= user.ExpectedSalary * 0.8m;
    }


    private static bool PassesRoleMismatchFilter(JobVacancy job, UserProfile user)
    {

        var targetsPm = TargetsProductManagement(user);

        if (!targetsPm) return true;

        var title = job.Title.ToLower();


        var hardBlocked = new[]
        {
            "bonus manager", "promo manager", "bonuses manager",
            "liveops manager", "live ops manager",
            "smm manager", "smm-manager",
            "sourcing manager", "procurement manager",
            "presale manager", "pre-sale manager",
            "account manager",
            "sales manager",
            "hr manager", "recruiter",
            "content manager",
            "office manager",
            "gambling manager", "casino manager",

            "production operations manager",

            "продакт-менеджер посуду",
            "продакт-менеджер (маркетинг)",
            "продакт-менеджер (сонячні",
        };

        if (hardBlocked.Any(blocked => title.Contains(blocked)))
            return false;

        return true;
    }

    private static bool TargetsProductManagement(UserProfile user)
    {

        if (!string.IsNullOrWhiteSpace(user.CvSummary))
        {
            try
            {
                using var doc = JsonDocument.Parse(user.CvSummary);
                if (doc.RootElement.TryGetProperty("target_roles", out var roles))
                {
                    foreach (var role in roles.EnumerateArray())
                    {
                        var r = role.GetString()?.ToLower() ?? "";
                        if (r.Contains("product")) return true;
                    }

                    return false;
                }
            }
            catch {  }
        }


        if (user.Category?.ToLower().Contains("product") == true) return true;
        if (user.Skills.Any(s => s.ToLower().Contains("product manager"))) return true;

        return false;
    }
}
