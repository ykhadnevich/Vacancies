using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Enums;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class ExperienceMatchCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.ExperienceMatch;

    public double Compute(JsonElement cv, JsonElement vacancy)
    {
        int requiredMonths = ReadRequiredMonths(vacancy);
        if (requiredMonths <= 0) return 1.0;

        int productionMonths = SumProductionMonths(cv);

        if (productionMonths >= requiredMonths) return 1.0;

        double ratio = (double)productionMonths / requiredMonths;


        bool careerSwitcher = cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("career_switcher", out var cs)
            && cs.ValueKind == JsonValueKind.True;

        return careerSwitcher ? Math.Max(0.5, ratio) : ratio;
    }


    private static int ReadRequiredMonths(JsonElement vacancy)
    {
        if (vacancy.ValueKind != JsonValueKind.Object) return 0;

        if (vacancy.TryGetProperty("min_years_experience", out var yEl)
            && yEl.ValueKind == JsonValueKind.Number)
        {


            int years = (int)Math.Round(yEl.GetDouble());
            if (years > 0) return years * 12;
        }


        if (vacancy.TryGetProperty("seniority_required", out var sEl)
            && sEl.ValueKind == JsonValueKind.String)
        {
            var level = SeniorityBoundaries.FromString(sEl.GetString());
            if (level != SeniorityLevel.NotSpecified)
            {
                int implied = SeniorityBoundaries.MinYears(level);
                if (implied > 0) return implied * 12;
            }
        }

        return 0;
    }

    private static int SumProductionMonths(JsonElement cv)
    {
        int total = 0;
        if (cv.ValueKind != JsonValueKind.Object) return 0;
        if (!cv.TryGetProperty("experience", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return 0;

        foreach (var exp in arr.EnumerateArray())
        {
            if (exp.ValueKind != JsonValueKind.Object) continue;
            var type = exp.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString() : null;
            if (type != "PRODUCTION" && type != "FREELANCE") continue;
            if (exp.TryGetProperty("duration_months", out var d) && d.ValueKind == JsonValueKind.Number)
                total += (int)Math.Round(d.GetDouble());
        }
        return total;
    }
}
