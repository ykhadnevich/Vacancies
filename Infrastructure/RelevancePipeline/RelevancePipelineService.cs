using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Infrastructure.RelevancePipeline.Prompts;
using Microsoft.Extensions.Logging;
using Vacancies.Domain.ValueObjects;

namespace Infrastructure.RelevancePipeline;


public class RelevancePipelineService : IRelevancePipeline
{
    private readonly IPreFilterService _preFilter;
    private readonly IRelevanceScoringService _scoring;
    private readonly IReasoningCacheService _reasoningCache;
    private readonly IJobReasoningServiceFactory _reasoningFactory;
    private readonly IReasoningContext _reasoningContext;
    private readonly IExperienceCapService _experienceCap;
    private readonly ILogger<RelevancePipelineService> _logger;

    private const int EagerReasoningTopN = 10;

    public RelevancePipelineService(
        IPreFilterService preFilter,
        IRelevanceScoringService scoring,
        IReasoningCacheService reasoningCache,
        IJobReasoningServiceFactory reasoningFactory,
        IReasoningContext reasoningContext,
        IExperienceCapService experienceCap,
        ILogger<RelevancePipelineService> logger)
    {
        _preFilter        = preFilter;
        _scoring          = scoring;
        _reasoningCache   = reasoningCache;
        _reasoningFactory = reasoningFactory;
        _reasoningContext  = reasoningContext;
        _experienceCap    = experienceCap;
        _logger           = logger;
    }

    public async Task<IReadOnlyList<JobVacancy>> RunAsync(
        IReadOnlyList<JobVacancy> jobs,
        UserProfile user,
        CancellationToken ct = default)
    {

        var preFiltered = await _preFilter.FilterAsync(jobs, user, ct);
        if (!preFiltered.Any()) return preFiltered;


        var reasoning = _reasoningFactory.Get(_reasoningContext.Provider);


        if (!reasoning.SupportsFullBatch)
        {
            var userText = BuildUserProfileText(user);
            var scoringInputs = preFiltered
                .Select(j => new JobScoringInput(j.Id, j.Title, j.Company, j.Description))
                .ToList();

            var scores   = await _scoring.ScoreJobsAsync(scoringInputs, userText, ct);
            var scoreMap = scores.ToDictionary(s => s.JobId);

            foreach (var job in preFiltered)
            {
                if (scoreMap.TryGetValue(job.Id, out var scored))
                    job.SetRelevanceScore(new RelevanceScore(scored.Score, ScoringStage.MlBiEncoder));
            }
        }

        var ranked = preFiltered
            .OrderByDescending(j => j.RelevanceScore?.Value ?? 0)
            .ToList();


        string CvRawSlice() => user.CvRawText![..Math.Min(5000, user.CvRawText!.Length)];
        var cvText = _reasoningContext.CvVersion switch
        {
            Application.Common.Enums.CvVersionPreference.Structured =>
                user.CvSummary!,
            Application.Common.Enums.CvVersionPreference.Raw =>
                !string.IsNullOrEmpty(user.CvRawText) ? CvRawSlice() : BuildUserProfileText(user),
            _  =>
                !string.IsNullOrWhiteSpace(user.CvSummary)
                    ? user.CvSummary
                    : !string.IsNullOrEmpty(user.CvRawText)
                        ? CvRawSlice()
                        : BuildUserProfileText(user)
        };


        List<JobVacancy> reasoningJobs;
        List<JobVacancy> restJobs;
        if (reasoning.SupportsFullBatch)
        {
            reasoningJobs = ranked;
            restJobs      = new List<JobVacancy>();
        }
        else
        {
            reasoningJobs = ranked.Take(EagerReasoningTopN).ToList();
            restJobs      = ranked.Skip(EagerReasoningTopN).ToList();
        }


        var cacheModelPrefix = GeminiReasoningProvider.BuildModelVersion(_reasoningContext.ScoringModel);
        var cacheResults = new Dictionary<Guid, CachedReason?>();
        foreach (var job in reasoningJobs)
            cacheResults[job.Id] = await _reasoningCache.GetReasonAsync(
                user.CvVersionId, job.Id, ct, requiredModelVersionPrefix: cacheModelPrefix);


        var missedJobs = reasoningJobs.Where(j => cacheResults[j.Id] is null).ToList();

        ReasoningResult[] llmResults;
        if (reasoning.SupportsFullBatch && missedJobs.Count > 0)
        {
            var tasks = missedJobs.Select(job => reasoning.GenerateReasonAsync(
                cvText, job.Title, job.Company, job.Description ?? string.Empty,
                job.RelevanceScore?.Value ?? 0, ct));
            llmResults = await Task.WhenAll(tasks);
        }
        else
        {
            llmResults = new ReasoningResult[missedJobs.Count];
            for (var i = 0; i < missedJobs.Count; i++)
            {
                var job = missedJobs[i];
                llmResults[i] = await reasoning.GenerateReasonAsync(
                    cvText, job.Title, job.Company, job.Description ?? string.Empty,
                    job.RelevanceScore?.Value ?? 0, ct);
            }
        }


        var totalIn  = llmResults.Sum(r => r.InputTokens);
        var totalOut = llmResults.Sum(r => r.OutputTokens);
        if (totalIn > 0 || totalOut > 0)
            _logger.LogInformation(
                "Gemini analysis complete — vacancies analysed: {Count} | input: {In} | output: {Out} | total tokens: {Total}",
                missedJobs.Count, totalIn, totalOut, totalIn + totalOut);


        var llmResultMap = new Dictionary<Guid, ReasoningResult>();
        for (var i = 0; i < missedJobs.Count; i++)
        {
            var job    = missedJobs[i];
            var result = llmResults[i];
            llmResultMap[job.Id] = result;

            if (!string.IsNullOrEmpty(result.Reason)
                && result.ModelVersion != "rule-based-v1"
                && result.ModelVersion != "gemini-empty")
                await _reasoningCache.SaveReasonAsync(
                    user.CvVersionId, job.Id,
                    result.Reason, result.Score ?? job.RelevanceScore?.Value ?? 0, result.ModelVersion, ct);
        }


        var roleYears = _experienceCap.ComputeRoleWeightedYears(cvText);
        var candidateTargetRoles = _experienceCap.ParseTargetRoles(cvText);
        var (careerSwitcher, technicalSkillsCount) = _experienceCap.ParseCareerSwitcherContext(cvText);

        _logger.LogInformation(
            "Cap service: roleYears={RoleYears} | targetRoles=[{Targets}] | careerSwitcher={Cs} techSkills={Ts}",
            roleYears is null ? "null (raw CV — no cap)" : $"PmPo={roleYears.PmPo:F1} Pmm={roleYears.Pmm:F1} BA={roleYears.BusinessAnalyst:F1} PM={roleYears.ProjectManager:F1} Dev={roleYears.Developer:F1}",
            string.Join(", ", candidateTargetRoles),
            careerSwitcher,
            technicalSkillsCount);


        var finalReasons = new Dictionary<Guid, string?>();

        foreach (var job in reasoningJobs)
        {
            float?  rawScore  = null;
            string? rawReason = null;

            if (cacheResults[job.Id] is { } hit && hit.Score.HasValue)
            {
                rawScore  = hit.Score.Value;
                rawReason = hit.Reason;
                _logger.LogDebug("Cap: [{Title}] — cache hit, rawScore={Score}", job.Title, rawScore);
            }
            else if (llmResultMap.TryGetValue(job.Id, out var res) && res.Score.HasValue)
            {
                rawScore  = res.Score.Value;
                rawReason = res.Reason;
                _logger.LogDebug("Cap: [{Title}] — fresh Gemini, rawScore={Score}", job.Title, rawScore);
            }

            if (rawScore is null)
            {
                _logger.LogDebug("Cap: [{Title}] — no score (skipped)", job.Title);
                continue;
            }

            float  finalScore  = rawScore.Value;
            string finalReason = rawReason ?? string.Empty;


            var multiCritCap = _experienceCap.TryApplyMultiCriticalCap(finalScore, finalReason);
            if (multiCritCap is not null)
            {
                _logger.LogDebug(
                    "Multi-critical cap: [{Title}] {Old:F1} → {New:F1}",
                    job.Title, finalScore, multiCritCap.Value.Score);
                finalScore  = multiCritCap.Value.Score;
                finalReason = multiCritCap.Value.Reason;
            }


            var mismatchCap = _experienceCap.TryApplyMismatchCap(
                finalScore, finalReason, job.Title, job.Description ?? string.Empty, candidateTargetRoles);
            if (mismatchCap is not null)
            {
                _logger.LogInformation(
                    "Mismatch cap: [{Title}] {Old:F1} → {New:F1}",
                    job.Title, finalScore, mismatchCap.Value.Score);
                finalScore  = mismatchCap.Value.Score;
                finalReason = mismatchCap.Value.Reason;
            }


            var platformCap = _experienceCap.TryApplyPlatformToolCap(
                finalScore, finalReason, job.Title, job.Description ?? string.Empty);
            if (platformCap is not null)
            {
                _logger.LogInformation(
                    "Platform-tool cap: [{Title}] {Old:F1} → {New:F1}",
                    job.Title, finalScore, platformCap.Value.Score);
                finalScore  = platformCap.Value.Score;
                finalReason = platformCap.Value.Reason;
            }


            var domainLock = _experienceCap.TryApplyDomainLockCap(
                finalScore, finalReason, job.Description ?? string.Empty);
            if (domainLock is not null)
            {
                _logger.LogInformation(
                    "Domain-lock cap: [{Title}] {Old:F1} → {New:F1}",
                    job.Title, finalScore, domainLock.Value.Score);
                finalScore  = domainLock.Value.Score;
                finalReason = domainLock.Value.Reason;
            }


            if (roleYears is not null && !string.IsNullOrEmpty(job.Title))
            {
                var capped = _experienceCap.TryApplyCap(
                    finalScore, finalReason,
                    job.Title, job.Description ?? string.Empty,
                    roleYears, careerSwitcher, technicalSkillsCount);

                if (capped is not null)
                {
                    _logger.LogInformation(
                        "Cap applied: [{Title}] {OldScore:F1} → {NewScore:F1}",
                        job.Title, finalScore, capped.Value.Score);
                    finalScore  = capped.Value.Score;
                    finalReason = capped.Value.Reason;
                }
            }

            job.SetRelevanceScore(new RelevanceScore(finalScore, ScoringStage.Gemini));
            finalReasons[job.Id] = finalReason;
        }


        foreach (var job in reasoningJobs)
        {
            if (finalReasons.TryGetValue(job.Id, out var reason) && !string.IsNullOrEmpty(reason)
                && reason != "rule-based-v1" && reason != "gemini-empty")
                job.SetReason(reason);
            else
                job.SetReason(BuildInstantTemplateReason(job, user));
        }


        foreach (var job in restJobs)
            job.SetReason(string.Empty);


        foreach (var job in ranked)
        {
            if (job.RelevanceScore?.Stage == ScoringStage.Gemini) continue;

            var calibrated = CalibrateScoreFromReason(job.Reason, job.RelevanceScore?.Value ?? 0);
            if (calibrated.HasValue)
                job.SetRelevanceScore(new RelevanceScore((float)calibrated.Value, ScoringStage.LlmCalibrated));
        }


        if (_reasoningContext.IncludeCompetitionSignals)
        foreach (var job in ranked)
        {
            var score = job.RelevanceScore?.Value;
            if (score is null) continue;

            var modifier = 0f;


            modifier += job.ApplicantCount switch
            {
                null           =>  0f,
                <= 5           =>  4f,
                <= 15          =>  2f,
                <= 50          =>  0f,
                <= 150         => -2f,
                _              => -4f
            };


            if (job.RecruiterRespondsQuickly == true)  modifier += 2f;
            if (job.RecruiterRespondsQuickly == false)  modifier -= 1f;

            if (modifier == 0f) continue;

            var adjusted = Math.Clamp(score.Value + modifier, 0f, 100f);


            var p5Note = new System.Text.StringBuilder();
            if (job.ApplicantCount.HasValue)
            {
                var competition = job.ApplicantCount.Value switch
                {
                    <= 5   => "дуже мало конкурентів",
                    <= 15  => "мало конкурентів",
                    <= 50  => "середня конкуренція",
                    <= 150 => "висока конкуренція",
                    _      => "дуже висока конкуренція"
                };
                p5Note.Append($"{job.ApplicantCount} відгуків ({competition})");
            }
            if (job.RecruiterRespondsQuickly == true)
                p5Note.Append(p5Note.Length > 0 ? " · рекрутер відповідає швидко" : "рекрутер відповідає швидко");
            else if (job.RecruiterRespondsQuickly == false)
                p5Note.Append(p5Note.Length > 0 ? " · рекрутер відповідає повільно" : "рекрутер відповідає повільно");

            if (p5Note.Length > 0)
            {
                var currentReason = job.Reason ?? string.Empty;
                job.SetReason(currentReason.TrimEnd() + $"\nP5: {p5Note}");
            }
            _logger.LogDebug(
                "P5 modifier [{Title}]: applicants={A} quick={Q} → {Delta:+0;-0} ({Old:F1}→{New:F1})",
                job.Title, job.ApplicantCount, job.RecruiterRespondsQuickly,
                modifier, score.Value, adjusted);
            job.SetRelevanceScore(new RelevanceScore(adjusted, job.RelevanceScore!.Stage));


            var refreshedReason = ExperienceCapService.RewriteVerdictInReason(job.Reason ?? string.Empty, adjusted);
            job.SetReason(refreshedReason);
        }


        if (_reasoningContext.IncludeCompetitionSignals)
        foreach (var job in ranked)
        {
            var score = job.RelevanceScore?.Value;
            if (score is null) continue;
            var finalCap = _experienceCap.TryApplyMultiCriticalCap(score.Value, job.Reason ?? string.Empty);
            if (finalCap is not null)
            {
                job.SetRelevanceScore(new RelevanceScore(finalCap.Value.Score, job.RelevanceScore!.Stage));
                job.SetReason(finalCap.Value.Reason);
            }
        }


        var now = DateTime.UtcNow;
        if (_reasoningContext.IncludeRecencyDecay)
        foreach (var job in reasoningJobs)
        {
            var score = job.RelevanceScore?.Value;
            if (score is null) continue;


            if (job.Source == JobSource.Jooble || job.Source == JobSource.Manual)
                continue;

            var ageDays = (now - job.PublishedAt).TotalDays;
            var decay = ageDays switch
            {
                <= 7  => 1.00f,
                <= 14 => 0.95f,
                <= 21 => 0.88f,
                <= 30 => 0.80f,
                <= 45 => 0.70f,
                <= 60 => 0.58f,
                <= 90 => 0.45f,
                _     => 0.30f
            };

            if (decay < 1.0f)
            {
                var decayed = MathF.Round(score.Value * decay, 1);
                _logger.LogDebug(
                    "Recency decay [{Title}]: {Score:F1} × {Decay} = {Decayed:F1} (age {Days:F0}d)",
                    job.Title, score.Value, decay, decayed, ageDays);
                job.SetRelevanceScore(new RelevanceScore(decayed, job.RelevanceScore!.Stage));


                var refreshed = ExperienceCapService.RewriteVerdictInReason(job.Reason ?? string.Empty, decayed);
                job.SetReason(refreshed);
            }
        }


        if (_reasoningContext.IncludeRecencyDecay)
        foreach (var job in reasoningJobs)
        {
            var score = job.RelevanceScore?.Value;
            if (score is null) continue;
            var finalCap = _experienceCap.TryApplyMultiCriticalCap(score.Value, job.Reason ?? string.Empty);
            if (finalCap is not null)
            {
                job.SetRelevanceScore(new RelevanceScore(finalCap.Value.Score, job.RelevanceScore!.Stage));
                job.SetReason(finalCap.Value.Reason);
            }
        }


        SpreadTiedScores(reasoningJobs);

        var reranked = reasoningJobs
            .OrderByDescending(j => j.RelevanceScore?.Value ?? 0)
            .Concat(restJobs)
            .ToList();

        return reranked;
    }


    private static void SpreadTiedScores(IReadOnlyList<JobVacancy> jobs)
    {

        var tieGroups = jobs
            .Where(j => j.RelevanceScore?.Stage == ScoringStage.Gemini)
            .GroupBy(j => (int)Math.Round(j.RelevanceScore!.Value))
            .Where(g => g.Count() > 1);

        foreach (var group in tieGroups)
        {

            static int MatchedCount(string? reason)
            {
                if (string.IsNullOrEmpty(reason)) return 0;
                var matchLine = reason.Split('\n')
                    .FirstOrDefault(l => l.StartsWith("Matched:"));
                if (matchLine is null || matchLine.Contains("none")) return 0;
                return matchLine.Count(c => c == ',') + 1;
            }

            var ordered = group
                .OrderByDescending(j => MatchedCount(j.Reason))
                .ThenByDescending(j => j.Description?.Length ?? 0)
                .ThenBy(j => j.Id)
                .ToList();


            var topScore = ordered[0].RelevanceScore!.Value;
            for (var i = 0; i < ordered.Count; i++)
            {
                var newScore = Math.Clamp(topScore - i * 0.5f, 0f, 100f);
                ordered[i].SetRelevanceScore(new RelevanceScore(newScore, ScoringStage.Gemini));


                var refreshedReason = ExperienceCapService.RewriteVerdictInReason(
                    ordered[i].Reason ?? string.Empty, newScore);
                ordered[i].SetReason(refreshedReason);
            }
        }
    }

    private static string BuildUserProfileText(UserProfile user)
    {
        if (!string.IsNullOrWhiteSpace(user.CvSummary))
            return user.CvSummary;

        var parts = new List<string>();
        if (!string.IsNullOrEmpty(user.Category)) parts.Add(user.Category);
        if (user.Skills.Any()) parts.Add(string.Join(", ", user.Skills));
        if (user.SeniorityLevel != SeniorityLevel.NotSpecified)
            parts.Add(user.SeniorityLevel.ToString());
        if (!string.IsNullOrEmpty(user.CvRawText))
            parts.Add(user.CvRawText[..Math.Min(500, user.CvRawText.Length)]);
        return string.Join(". ", parts);
    }


    private static double? CalibrateScoreFromReason(string? reason, double rawScore)
    {
        if (string.IsNullOrEmpty(reason)) return null;

        var verdictLine = reason
            .Split('\n')
            .Select(l => l.Trim())
            .FirstOrDefault(l => l.StartsWith("Verdict:"));

        if (verdictLine is null) return null;

        var verdict = verdictLine["Verdict:".Length..].Trim();

        var (min, max) = verdict switch
        {
            "strong_fit"  => (85.0, 100.0),
            "good_fit"    => (65.0,  84.0),
            "partial_fit" => (35.0,  64.0),
            "weak_fit"    => ( 0.0,  34.0),
            _             => (-1.0,  -1.0)
        };

        if (min < 0) return null;

        var normalized = Math.Clamp(rawScore / 100.0, 0.0, 1.0);
        return Math.Round(min + normalized * (max - min), 1);
    }


    private static string BuildInstantTemplateReason(JobVacancy job, UserProfile user)
    {
        var jobText = $"{job.Title} {job.Description}".ToLowerInvariant();

        if (user.Skills.Any())
        {
            var matchedSkills = user.Skills
                .Where(skill =>
                {
                    var escaped = Regex.Escape(skill.ToLowerInvariant());
                    return Regex.IsMatch(jobText, $@"\b{escaped}\b");
                })
                .Take(4)
                .ToList();

            if (matchedSkills.Any())
                return $"Знайдено збіг по навичках: {string.Join(", ", matchedSkills)}. Детальний аналіз генерується...";
        }

        var score = job.RelevanceScore?.Value ?? 0;
        return score >= 70
            ? "Хороший збіг з вашим профілем. Детальний аналіз генерується..."
            : "Детальний аналіз генерується...";
    }
}
