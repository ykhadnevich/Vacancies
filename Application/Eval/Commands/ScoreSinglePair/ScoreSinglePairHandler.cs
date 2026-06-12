using System.Text.Json;
using Application.Common.Interfaces;
using Application.DTOs.Eval;
using MediatR;

namespace Application.Eval.Commands.ScoreSinglePair;


public sealed class ScoreSinglePairHandler
    : IRequestHandler<ScoreSinglePairCommand, EvalPairResultDto>
{
    private readonly IEvalDataSource _data;
    private readonly IScoringService _scoring;

    public ScoreSinglePairHandler(IEvalDataSource data, IScoringService scoring)
    {
        _data = data;
        _scoring = scoring;
    }

    public async Task<EvalPairResultDto> Handle(ScoreSinglePairCommand request, CancellationToken ct)
    {
        var cvJson = await _data.GetCvSummaryAsync(request.CvId, ct)
            ?? throw new KeyNotFoundException(
                $"CV '{request.CvId}' not found in gold set.");

        var vacJson = await _data.GetVacancyAnalysisAsync(request.VacancyId, ct)
            ?? throw new KeyNotFoundException(
                $"Vacancy '{request.VacancyId}' has no normalized analysis on disk. " +
                "Run EvalTool evaluate-vacancies first.");

        var result = await _scoring.ScoreAsync(
            request.CvId, request.VacancyId, cvJson, vacJson, ct);

        return new EvalPairResultDto(
            CvId: result.CvId,
            VacancyId: result.VacancyId,
            VacancyTitle: ExtractRoleTitleEn(vacJson),
            Rank: 1,
            Score: result.Score,
            Verdict: BucketVerdict(result.Score),
            SkillMatch:       result.SubScores.SkillMatch,
            SeniorityMatch:   result.SubScores.SeniorityMatch,
            ExperienceMatch:  result.SubScores.ExperienceMatch,
            LanguageMatch:    result.SubScores.LanguageMatch,
            EducationMatch:   result.SubScores.EducationMatch,
            RoleIntentMatch:  result.SubScores.RoleIntentMatch,
            DomainAlignment:  result.SubScores.DomainAlignment,
            AntiFlagPenalty:  result.AntiFlagPenalty,
            ReasonEn:         result.ReasonEn,
            ReasonUk:         result.ReasonUk,
            MatchedSkills:    result.Evidence.MatchedSkills,
            MissingMustHaves: result.Evidence.MissingMustHaves,
            TriggeredAntiFlags: result.Evidence.TriggeredAntiFlags);
    }

    private static string BucketVerdict(double score) =>
        score >= 0.75 ? "Strong"   :
        score >= 0.50 ? "Partial"  :
        score >= 0.25 ? "Weak"     : "Mismatch";


    private static string ExtractRoleTitleEn(string vacancyJson)
    {
        try
        {
            using var doc = JsonDocument.Parse(vacancyJson);
            if (doc.RootElement.TryGetProperty("role_title", out var rt)
                && rt.ValueKind == JsonValueKind.Object
                && rt.TryGetProperty("en", out var enEl)
                && enEl.ValueKind == JsonValueKind.String)
            {
                return enEl.GetString() ?? "?";
            }
        }
        catch (JsonException) {  }
        return "?";
    }
}
