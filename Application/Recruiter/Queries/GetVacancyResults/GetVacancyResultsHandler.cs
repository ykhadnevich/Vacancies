using System.Text.Json;
using Application.DTOs.Recruiter;
using Domain.Interfaces.Repositories;
using Domain.Scoring;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Recruiter.Queries.GetVacancyResults;

public sealed class GetVacancyResultsHandler
    : IRequestHandler<GetVacancyResultsQuery, IReadOnlyList<CandidateAnalysisResultDto>>
{
    private readonly ICandidateScoreRepository _scores;
    private readonly IRecruiterCandidateRepository _candidates;
    private readonly ILogger<GetVacancyResultsHandler> _logger;

    public GetVacancyResultsHandler(
        ICandidateScoreRepository scores,
        IRecruiterCandidateRepository candidates,
        ILogger<GetVacancyResultsHandler> logger)
    {
        _scores = scores;
        _candidates = candidates;
        _logger = logger;
    }

    public async Task<IReadOnlyList<CandidateAnalysisResultDto>> Handle(
        GetVacancyResultsQuery query, CancellationToken ct)
    {
        var rows = await _scores.GetForVacancyAndListAsync(
            query.VacancyId, query.CandidateListId, ct);
        if (rows.Count == 0)
            return Array.Empty<CandidateAnalysisResultDto>();

        var candidates = (await _candidates.GetByIdsAsync(
            rows.Select(r => r.RecruiterCandidateId).ToList(), ct))
            .ToDictionary(c => c.Id);

        var results = new List<CandidateAnalysisResultDto>(rows.Count);
        foreach (var row in rows)
        {
            ScoringResult? scoring = null;
            try
            {
                scoring = JsonSerializer.Deserialize<ScoringResult>(row.ScoringResultJson);
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex,
                    "Failed to deserialise ScoringResult for candidate {CandidateId} × vacancy {VacancyId}.",
                    row.RecruiterCandidateId, row.VacancyId);
            }

            candidates.TryGetValue(row.RecruiterCandidateId, out var candidate);

            // Flatten the SubScores record into a dictionary the frontend renders as bars.
            var subScores = scoring?.SubScores is null
                ? new Dictionary<string, double>()
                : new Dictionary<string, double>
                {
                    ["skill_match"]       = scoring.SubScores.SkillMatch,
                    ["seniority_match"]   = scoring.SubScores.SeniorityMatch,
                    ["experience_match"]  = scoring.SubScores.ExperienceMatch,
                    ["language_match"]    = scoring.SubScores.LanguageMatch,
                    ["education_match"]   = scoring.SubScores.EducationMatch,
                    ["role_intent_match"] = scoring.SubScores.RoleIntentMatch,
                    ["domain_alignment"]  = scoring.SubScores.DomainAlignment,
                };

            // Same per-million pricing the cost-log telemetry uses (see CostBreakdown).
            var inputTokens  = scoring?.InputTokens  ?? 0;
            var outputTokens = scoring?.OutputTokens ?? 0;
            var estCost = (inputTokens / 1_000_000.0) * 0.30
                        + (outputTokens / 1_000_000.0) * 2.50;

            results.Add(new CandidateAnalysisResultDto(
                CandidateId:        row.RecruiterCandidateId,
                CandidateName:      candidate?.CandidateName,
                Score:              row.Score,
                Verdict:            scoring?.Verdict.ToString() ?? "Unknown",
                ReasonUk:           scoring?.ReasonUk,
                ReasonEn:           scoring?.ReasonEn,
                MatchedSkills:      scoring?.Evidence?.MatchedSkills ?? Array.Empty<string>(),
                MissingMustHaves:   scoring?.Evidence?.MissingMustHaves ?? Array.Empty<string>(),
                TriggeredAntiFlags: scoring?.Evidence?.TriggeredAntiFlags ?? Array.Empty<string>(),
                SubScores:          subScores,
                AntiFlagPenalty:    scoring?.AntiFlagPenalty ?? 1.0,
                Confidence:         scoring?.Confidence,
                InputTokens:        inputTokens,
                OutputTokens:       outputTokens,
                EstimatedCostUsd:   estCost,
                ModelVersion:       scoring?.ModelVersion ?? row.ScoringVersion,
                ScoredAt:           row.ScoredAt));
        }

        return results;
    }
}
