using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class SkillMatchCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.SkillMatch;

    public double Compute(JsonElement cv, JsonElement vacancy)
    {
        var must = ReadStringSet(vacancy, "must_have_skills");
        var nice = ReadStringSet(vacancy, "nice_to_have_skills");
        var cvSkills = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        cvSkills.UnionWith(ReadStringSet(cv, "technical_skills"));
        cvSkills.UnionWith(ReadStringSet(cv, "domain_skills"));


        if (SkillExpansionMatcher.TryBuildCvLookup(cv, out var cvLookup))
        {
            double mustSum = 0.0;
            foreach (var m in must) mustSum += SkillExpansionMatcher.Score(m, vacancy, cvLookup);
            double niceSum = 0.0;
            foreach (var n in nice) niceSum += SkillExpansionMatcher.Score(n, vacancy, cvLookup);

            double baseMatch = must.Count == 0 ? 1.0 : mustSum / must.Count;
            double niceBonus = nice.Count == 0
                ? 0.0
                : ScoringConstants.SkillMatch.NiceToHaveBonus * niceSum / nice.Count;
            return Math.Min(1.0, baseMatch + niceBonus);
        }


        var cvExpanded = SkillCanonicalizer.ExpandAll(cvSkills);
        int matchedMust = must.Count(m => SkillCanonicalizer.Matches(m, cvExpanded));
        int matchedNice = nice.Count(n => SkillCanonicalizer.Matches(n, cvExpanded));
        double bm = must.Count == 0 ? 1.0 : (double)matchedMust / must.Count;
        double nb = nice.Count == 0
            ? 0.0
            : ScoringConstants.SkillMatch.NiceToHaveBonus * matchedNice / nice.Count;
        return Math.Min(1.0, bm + nb);
    }

    private static HashSet<string> ReadStringSet(JsonElement obj, string field)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (obj.ValueKind != JsonValueKind.Object) return set;
        if (!obj.TryGetProperty(field, out var arr)) return set;
        if (arr.ValueKind != JsonValueKind.Array) return set;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s)) set.Add(s);
            }
        }
        return set;
    }
}
