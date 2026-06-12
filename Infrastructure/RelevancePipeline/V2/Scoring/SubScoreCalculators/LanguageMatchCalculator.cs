using System.Text.Json;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class LanguageMatchCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.LanguageMatch;

    private static readonly Dictionary<string, int> Ladder = new(StringComparer.OrdinalIgnoreCase)
    {
        ["A1"] = 1, ["A2"] = 2, ["B1"] = 3, ["B2"] = 4, ["C1"] = 5, ["C2"] = 6, ["native"] = 7,
        ["not_specified"] = 3
    };

    public double Compute(JsonElement cv, JsonElement vacancy)
    {
        var req = ReadString(vacancy, "english_required") ?? "not_specified";
        var have = ReadString(cv, "english_level") ?? "not_specified";

        if (!Ladder.TryGetValue(req, out var reqInt)) reqInt = 3;
        if (!Ladder.TryGetValue(have, out var haveInt)) haveInt = 3;

        int delta = haveInt - reqInt;
        return delta switch
        {
            >= 0 => 1.0,
            -1   => 0.7,
            -2   => 0.4,
            _    => 0.1
        };
    }

    private static string? ReadString(JsonElement obj, string field)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(field, out var v)) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString()?.Trim();
    }
}
