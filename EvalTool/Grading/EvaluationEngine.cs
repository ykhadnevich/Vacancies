using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace EvalTool.Grading;


public sealed class EvaluationEngine
{
    private readonly ILogger<EvaluationEngine> _logger;
    private readonly List<IFieldGrader> _graders;
    private readonly ExperienceGrader _experienceGrader;

    public EvaluationEngine(ILogger<EvaluationEngine> logger)
    {
        _logger = logger;
        _graders = BuildGraders();
        _experienceGrader = new ExperienceGrader();
    }


    public CaseScores Grade(string caseId, string actualJson, string expectedJson)
    {
        var actualDoc = JsonDocument.Parse(actualJson);
        var expectedDoc = JsonDocument.Parse(expectedJson);
        var actualRoot = actualDoc.RootElement;
        var expectedRoot = expectedDoc.RootElement;

        var scores = new Dictionary<string, double>();


        foreach (var grader in _graders)
        {
            JsonElement? actualField = TryGetByPath(actualRoot, grader.FieldPath);
            JsonElement? expectedField = TryGetByPath(expectedRoot, grader.FieldPath);
            scores[grader.FieldPath] = grader.Grade(actualField, expectedField);
        }


        JsonElement? actualExp = TryGetByPath(actualRoot, "experience");
        JsonElement? expectedExp = TryGetByPath(expectedRoot, "experience");
        var expScores = _experienceGrader.Grade(actualExp, expectedExp);
        scores["experience.titles_f1"]      = expScores.TitlesF1;
        scores["experience.types_acc"]      = expScores.TypesAccuracy;
        scores["experience.durations_acc"]  = expScores.DurationsAccuracy;
        scores["experience.years_ago_acc"]  = expScores.YearsAgoAccuracy;


        double overall = scores.Count == 0 ? 0 : scores.Values.Average();

        return new CaseScores(caseId, scores, overall);
    }


    public EvaluationReport Aggregate(string version, List<CaseScores> perCase)
    {
        if (perCase.Count == 0)
        {
            return new EvaluationReport(version, DateTime.UtcNow,
                perCase, new Dictionary<string, double>(), 0);
        }

        var allFieldNames = perCase
            .SelectMany(c => c.FieldScores.Keys)
            .Distinct()
            .OrderBy(k => k)
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

        double overall = perCase.Select(c => c.Overall).Average();
        return new EvaluationReport(version, DateTime.UtcNow, perCase, perField, overall);
    }


    private static List<IFieldGrader> BuildGraders()
    {


        return new List<IFieldGrader>
        {

            new ExactStringGrader("seniority"),
            new ExactStringGrader("english_level"),


            new BooleanGrader("has_real_product_experience"),
            new BooleanGrader("career_switcher"),


            new RoleArrayF1Grader("target_roles"),
            new StringArrayF1Grader("domain_skills"),
            new StringArrayF1Grader("technical_skills"),
            new StringArrayF1Grader("unverified_skills"),


            new LanguagesGrader("languages"),


            new ExactStringGrader("education.degree"),
            new ExactStringGrader("education.field"),
            new BooleanGrader("education.is_relevant"),
            new ExactStringGrader("education.status"),
            new IntegerToleranceGrader("education.current_year", tolerance: 1),
            new IntegerToleranceGrader("education.graduation_year", tolerance: 1),
        };
    }


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
