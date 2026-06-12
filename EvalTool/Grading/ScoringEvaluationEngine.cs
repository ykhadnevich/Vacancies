using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Microsoft.Extensions.Logging;

namespace EvalTool.Grading;


public sealed class ScoringEvaluationEngine
{

    private static readonly Dictionary<SubScoreAxis, double> Weights = new()
    {
        [SubScoreAxis.SkillMatch]       = 0.30,
        [SubScoreAxis.SeniorityMatch]   = 0.15,
        [SubScoreAxis.ExperienceMatch]  = 0.15,
        [SubScoreAxis.LanguageMatch]    = 0.10,
        [SubScoreAxis.EducationMatch]   = 0.05,
        [SubScoreAxis.RoleIntentMatch]  = 0.15,
        [SubScoreAxis.DomainAlignment]  = 0.10
    };

    private readonly IReadOnlyDictionary<SubScoreAxis, ISubScoreCalculator> _calculators;
    private readonly ILogger<ScoringEvaluationEngine> _logger;

    public ScoringEvaluationEngine(
        IEnumerable<ISubScoreCalculator> calculators,
        ILogger<ScoringEvaluationEngine> logger)
    {
        _calculators = calculators.ToDictionary(c => c.Axis);
        _logger = logger;

        foreach (SubScoreAxis axis in Enum.GetValues<SubScoreAxis>())
        {
            if (!_calculators.ContainsKey(axis))
                throw new InvalidOperationException(
                    $"Missing ISubScoreCalculator for axis '{axis}'. Check DI registration.");
        }
    }


    public CaseScores Grade(
        string caseId,
        string goldCvSummaryJson,
        string goldVacancyAnalysisJson,
        string pipelineScoringResultJson)
    {
        var goldCv = JsonDocument.Parse(goldCvSummaryJson).RootElement;
        var goldVac = JsonDocument.Parse(goldVacancyAnalysisJson).RootElement;
        var pipeline = JsonDocument.Parse(pipelineScoringResultJson).RootElement;


        var idealSubs = new Dictionary<SubScoreAxis, double>();
        foreach (SubScoreAxis axis in Enum.GetValues<SubScoreAxis>())
            idealSubs[axis] = Math.Clamp(_calculators[axis].Compute(goldCv, goldVac), 0.0, 1.0);


        var idealWeightedSum = Weights.Sum(kv => idealSubs[kv.Key] * kv.Value);
        var idealScore = Math.Clamp(idealWeightedSum, 0.0, 1.0);


        var pipelineScore = pipeline.TryGetProperty("score", out var sc)
            ? Math.Clamp(sc.GetDouble(), 0.0, 1.0)
            : 0.0;
        var pipelineSubs = ReadPipelineSubScores(pipeline);
        var antiFlagPenalty = pipeline.TryGetProperty("anti_flag_penalty", out var af)
            ? af.GetDouble()
            : 1.0;


        var scores = new Dictionary<string, double>
        {

            ["score.mae"]                     = Math.Abs(pipelineScore - idealScore),
            ["score.signed_diff"]             = pipelineScore - idealScore,
            ["score.verdict_match"]           = VerdictFromScore(pipelineScore) == VerdictFromScore(idealScore) ? 1.0 : 0.0,
            ["score.anti_flag_active"]        = antiFlagPenalty < 1.0 ? 1.0 : 0.0,
            ["score.ideal_value"]             = idealScore,
            ["score.pipeline_value"]          = pipelineScore,


            ["score.skill_match.bias"]        = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.SkillMatch),
            ["score.seniority_match.bias"]    = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.SeniorityMatch),
            ["score.experience_match.bias"]   = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.ExperienceMatch),
            ["score.language_match.bias"]     = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.LanguageMatch),
            ["score.education_match.bias"]    = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.EducationMatch),
            ["score.role_intent_match.bias"]  = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.RoleIntentMatch),
            ["score.domain_alignment.bias"]   = SafeSubDiff(pipelineSubs, idealSubs, SubScoreAxis.DomainAlignment),
        };


        var overall = 1.0 - scores["score.mae"];

        return new CaseScores(
            CaseId: caseId,
            FieldScores: scores,
            Overall: overall);
    }

    private static Dictionary<SubScoreAxis, double> ReadPipelineSubScores(JsonElement pipeline)
    {
        var result = new Dictionary<SubScoreAxis, double>();
        if (!pipeline.TryGetProperty("sub_scores", out var subs)
            || subs.ValueKind != JsonValueKind.Object)
            return result;

        var nameMap = new Dictionary<string, SubScoreAxis>(StringComparer.OrdinalIgnoreCase)
        {
            ["skill_match"]       = SubScoreAxis.SkillMatch,
            ["seniority_match"]   = SubScoreAxis.SeniorityMatch,
            ["experience_match"]  = SubScoreAxis.ExperienceMatch,
            ["language_match"]    = SubScoreAxis.LanguageMatch,
            ["education_match"]   = SubScoreAxis.EducationMatch,
            ["role_intent_match"] = SubScoreAxis.RoleIntentMatch,
            ["domain_alignment"]  = SubScoreAxis.DomainAlignment,
        };

        foreach (var prop in subs.EnumerateObject())
        {
            if (!nameMap.TryGetValue(prop.Name, out var axis)) continue;
            if (prop.Value.ValueKind != JsonValueKind.Number) continue;
            result[axis] = Math.Clamp(prop.Value.GetDouble(), 0.0, 1.0);
        }
        return result;
    }

    private static double SafeSubDiff(
        Dictionary<SubScoreAxis, double> pipeline,
        Dictionary<SubScoreAxis, double> ideal,
        SubScoreAxis axis)
    {
        var p = pipeline.TryGetValue(axis, out var pv) ? pv : 0.0;
        var i = ideal.TryGetValue(axis, out var iv) ? iv : 0.0;
        return p - i;
    }

    private static string VerdictFromScore(double score) =>
        score >= 0.75 ? "Strong"
            : score >= 0.50 ? "Partial"
            : score >= 0.25 ? "Weak"
            : "Mismatch";
}
