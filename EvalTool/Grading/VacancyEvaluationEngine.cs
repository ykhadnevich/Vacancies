using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EvalTool.Grading;


public sealed class VacancyEvaluationEngine
{
    private readonly ILogger<VacancyEvaluationEngine> _logger;
    private readonly List<IFieldGrader> _graders;

    public VacancyEvaluationEngine(ILogger<VacancyEvaluationEngine> logger)
    {
        _logger = logger;
        _graders = BuildGraders();
    }


    public CaseScores Grade(string caseId, string actualJson, string expectedJson)
    {
        using var actualDoc = JsonDocument.Parse(actualJson);
        using var expectedDoc = JsonDocument.Parse(expectedJson);
        var actualRoot = actualDoc.RootElement;
        var expectedRoot = expectedDoc.RootElement;

        var scores = new Dictionary<string, double>();
        foreach (var grader in _graders)
        {
            JsonElement? actualField = TryGetByPath(actualRoot, grader.FieldPath);
            JsonElement? expectedField = TryGetByPath(expectedRoot, grader.FieldPath);
            scores[grader.FieldPath] = grader.Grade(actualField, expectedField);
        }

        double overall = scores.Count == 0 ? 0 : scores.Values.Average();
        return new CaseScores(caseId, scores, overall);
    }

    public EvaluationReport Aggregate(string version, List<CaseScores> perCase)
    {
        if (perCase.Count == 0)
            return new EvaluationReport(version, DateTime.UtcNow,
                perCase, new Dictionary<string, double>(), 0);

        var allFieldNames = perCase
            .SelectMany(c => c.FieldScores.Keys)
            .Distinct()
            .OrderBy(n => n)
            .ToList();

        var perField = new Dictionary<string, double>();
        foreach (var field in allFieldNames)
        {
            var values = perCase
                .Where(c => c.FieldScores.ContainsKey(field))
                .Select(c => c.FieldScores[field])
                .ToList();
            perField[field] = values.Count == 0 ? 0 : values.Average();
        }

        var overall = perField.Count == 0 ? 0 : perField.Values.Average();
        return new EvaluationReport(version, DateTime.UtcNow, perCase, perField, overall);
    }


    private static List<IFieldGrader> BuildGraders() => new()
    {

        new ExactStringGrader("source_language"),
        new ExactStringGrader("seniority_required"),
        new ExactStringGrader("education_required"),


        new CEFRToleranceGrader("english_required", tolerance: 1),


        new JaccardStringGrader("role_title.en"),
        new JaccardStringGrader("role_title.uk"),
        new ExactStringGrader("role_title_raw"),


        new IntegerToleranceGrader("min_years_experience", tolerance: 1),


        new ExactStringGrader("location.city_en"),
        new ExactStringGrader("location.city_uk"),
        new BooleanGrader("location.remote"),
        new BooleanGrader("location.hybrid"),


        new CommaTokenJaccardGrader("domain_context.en"),
        new CommaTokenJaccardGrader("domain_context.uk"),


        new TokenJaccardArrayFBetaGrader("must_have_skills",    beta: 2.0, matchThreshold: 0.5),
        new TokenJaccardArrayFBetaGrader("nice_to_have_skills", beta: 2.0, matchThreshold: 0.5),
        new StringArrayF1Grader("anti_requirements"),
    };

    private static JsonElement? TryGetByPath(JsonElement root, string path)
    {
        var parts = path.Split('.');
        var current = root;
        foreach (var part in parts)
        {
            if (current.ValueKind != JsonValueKind.Object) return null;
            if (!current.TryGetProperty(part, out var next)) return null;
            current = next;
        }
        return current;
    }
}
