using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class SeniorityMatchCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.SeniorityMatch;


    private static readonly Dictionary<(string req, string cv), double> Table = new()
    {

        [("intern","intern")] = 1.0,
        [("intern","junior")] = 1.0, [("intern","middle")] = 1.0, [("intern","senior")] = 1.0, [("intern","lead")] = 1.0,


        [("junior","intern")] = 0.5,
        [("junior","junior")] = 1.0,
        [("junior","middle")] = 1.0, [("junior","senior")] = 1.0, [("junior","lead")] = 1.0,


        [("middle","intern")] = 0.3,
        [("middle","junior")] = 0.7,
        [("middle","middle")] = 1.0,
        [("middle","senior")] = 1.0, [("middle","lead")] = 1.0,


        [("senior","intern")] = 0.1,
        [("senior","junior")] = 0.3,
        [("senior","middle")] = 0.7,
        [("senior","senior")] = 1.0,
        [("senior","lead")] = 1.0,


        [("lead","intern")] = 0.0,
        [("lead","junior")] = 0.1,
        [("lead","middle")] = 0.3,
        [("lead","senior")] = 0.7,
        [("lead","lead")] = 1.0,
    };

    public double Compute(JsonElement cv, JsonElement vacancy)
    {
        var required = DeriveRequired(vacancy);
        var have = ReadString(cv, "seniority");

        if (required is null || have is null
            || required == "not_specified" || have == "not_specified")
            return 0.7;

        return Table.TryGetValue((required, have), out var v) ? v : 0.3;
    }


    private static string? DeriveRequired(JsonElement vacancy)
    {
        if (vacancy.ValueKind != JsonValueKind.Object) return null;

        if (vacancy.TryGetProperty("min_years_experience", out var yEl)
            && yEl.ValueKind == JsonValueKind.Number)
        {
            int years = (int)Math.Round(yEl.GetDouble());
            if (years > 0)
            {
                return SeniorityBoundaries.ToCanonicalString(
                    SeniorityBoundaries.FromYears(years));
            }
        }


        var fromString = ReadString(vacancy, "seniority_required");
        if (string.IsNullOrEmpty(fromString)) return null;

        return SeniorityBoundaries.ToCanonicalString(
            SeniorityBoundaries.FromString(fromString));
    }

    private static string? ReadString(JsonElement obj, string field)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(field, out var v)) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString()?.ToLowerInvariant().Trim();
    }
}
