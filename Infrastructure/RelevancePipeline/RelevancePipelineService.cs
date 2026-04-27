using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.ValueObjects;
using Vacancies.Domain.ValueObjects;

namespace Infrastructure.RelevancePipeline;

public class RelevancePipelineService : IRelevancePipeline
{
    private readonly IPreFilterService _preFilter;
    private readonly IGeminiScoringService _geminiScoring;

    public RelevancePipelineService(
        IPreFilterService preFilter,
        IGeminiScoringService geminiScoring)
    {
        _preFilter = preFilter;
        _geminiScoring = geminiScoring;
    }

    public async Task<IReadOnlyList<JobVacancy>> RunAsync(
        IReadOnlyList<JobVacancy> jobs,
        UserProfile user,
        CancellationToken ct = default)
    {
        var preFiltered = await _preFilter.FilterAsync(jobs, user, ct);
        if (!preFiltered.Any()) return preFiltered;

        var userText = BuildUserProfileText(user);
        var jobInputs = preFiltered
            .Select(j => (j.Id, j.Title, j.Company, j.Description))
            .ToList();

        var scores = await _geminiScoring.ScoreJobsAsync(jobInputs, userText, ct);
        var scoreMap = scores.ToDictionary(s => s.JobId);

        foreach (var job in preFiltered)
        {
            if (scoreMap.TryGetValue(job.Id, out var scored))
            {
                job.SetRelevanceScore(new RelevanceScore(scored.Score, ScoringStage.LlmRerank));
            }
        }

        return preFiltered
            .OrderByDescending(j => j.RelevanceScore?.Value ?? 0)
            .ToList();
    }

    private static string BuildUserProfileText(UserProfile user)
    {
        var parts = new List<string>();
        if (!string.IsNullOrEmpty(user.Category)) parts.Add(user.Category);
        if (user.Skills.Any()) parts.Add(string.Join(", ", user.Skills));
        if (user.SeniorityLevel != SeniorityLevel.NotSpecified)
            parts.Add(user.SeniorityLevel.ToString());
        if (!string.IsNullOrEmpty(user.CvRawText))
            parts.Add(user.CvRawText[..Math.Min(500, user.CvRawText.Length)]);
        return string.Join(". ", parts);
    }
}
