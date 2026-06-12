using System.Text.Json;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class EducationMatchCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.EducationMatch;

    private static readonly Dictionary<string, int> Rank = new(StringComparer.OrdinalIgnoreCase)
    {
        ["none"] = 0, ["bachelor"] = 1, ["associate"] = 1,
        ["master"] = 2, ["phd"] = 3, ["not_specified"] = 0
    };

    public double Compute(JsonElement cv, JsonElement vacancy)
    {
        var req = ReadString(vacancy, "education_required") ?? "not_specified";
        if (!Rank.TryGetValue(req, out var reqRank)) reqRank = 0;

        var cvDegree = "none";
        bool isRelevant = true;
        if (cv.ValueKind == JsonValueKind.Object
            && cv.TryGetProperty("education", out var edu)
            && edu.ValueKind == JsonValueKind.Object)
        {
            if (edu.TryGetProperty("degree", out var d) && d.ValueKind == JsonValueKind.String)
                cvDegree = d.GetString() ?? "none";
            if (edu.TryGetProperty("is_relevant", out var r))
                isRelevant = r.ValueKind != JsonValueKind.False;
        }
        if (!Rank.TryGetValue(cvDegree, out var haveRank)) haveRank = 0;

        double baseScore;
        if (haveRank >= reqRank)
            baseScore = 1.0;
        else if (reqRank > 0)
            baseScore = 0.5 + 0.5 * ((double)haveRank / reqRank);
        else
            baseScore = 1.0;

        return isRelevant ? baseScore : baseScore * 0.85;
    }

    private static string? ReadString(JsonElement obj, string field)
    {
        if (obj.ValueKind != JsonValueKind.Object) return null;
        if (!obj.TryGetProperty(field, out var v)) return null;
        if (v.ValueKind != JsonValueKind.String) return null;
        return v.GetString()?.Trim();
    }
}
