using System.Text.Json;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public static class SkillExpansionMatcher
{
    public static bool TryBuildCvLookup(JsonElement cv, out Dictionary<string, double> lookup)
    {
        lookup = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase);
        if (cv.ValueKind != JsonValueKind.Object) return false;


        if (cv.TryGetProperty("_skills_expanded", out var expEl)
            && expEl.ValueKind == JsonValueKind.Object)
        {
            foreach (var prop in expEl.EnumerateObject())
            {
                if (prop.Value.ValueKind != JsonValueKind.Array) continue;
                foreach (var entry in prop.Value.EnumerateArray())
                {
                    if (entry.ValueKind != JsonValueKind.Object) continue;
                    if (!entry.TryGetProperty("term", out var tEl)
                        || tEl.ValueKind != JsonValueKind.String) continue;
                    var term = tEl.GetString();
                    if (string.IsNullOrWhiteSpace(term)) continue;
                    double conf = 1.0;
                    if (entry.TryGetProperty("confidence", out var cEl)
                        && cEl.ValueKind == JsonValueKind.Number)
                        conf = Math.Clamp(cEl.GetDouble(), 0.0, 1.0);

                    if (lookup.TryGetValue(term, out var existing))
                        lookup[term] = Math.Max(existing, conf);
                    else
                        lookup[term] = conf;
                }
            }
        }


        foreach (var field in new[] { "technical_skills", "domain_skills" })
        {
            if (!cv.TryGetProperty(field, out var arr)
                || arr.ValueKind != JsonValueKind.Array) continue;
            foreach (var entry in arr.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.String) continue;
                var term = entry.GetString()?.Trim();
                if (string.IsNullOrEmpty(term)) continue;
                if (lookup.TryGetValue(term, out var existing))
                    lookup[term] = Math.Max(existing, 1.0);
                else
                    lookup[term] = 1.0;
            }
        }

        return lookup.Count > 0;
    }


    public static JsonElement? TryGetVacancyExpansion(JsonElement vacancy, string mustHaveName)
    {
        if (vacancy.ValueKind != JsonValueKind.Object) return null;
        if (!vacancy.TryGetProperty("_must_haves_expanded", out var expEl)
            || expEl.ValueKind != JsonValueKind.Object) return null;
        if (!expEl.TryGetProperty(mustHaveName, out var arr)
            || arr.ValueKind != JsonValueKind.Array) return null;
        return arr;
    }


    public static double Score(string mhName, JsonElement vacancy, Dictionary<string, double> cvLookup)
    {
        var vacEntry = TryGetVacancyExpansion(vacancy, mhName);
        if (vacEntry is null)
            return BestMatch(mhName, 1.0, cvLookup);

        double best = 0.0;
        foreach (var item in vacEntry.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;
            if (!item.TryGetProperty("term", out var tEl)
                || tEl.ValueKind != JsonValueKind.String) continue;
            var term = tEl.GetString();
            if (string.IsNullOrWhiteSpace(term)) continue;

            double vacConf = 1.0;
            if (item.TryGetProperty("confidence", out var cEl)
                && cEl.ValueKind == JsonValueKind.Number)
                vacConf = Math.Clamp(cEl.GetDouble(), 0.0, 1.0);

            double w = BestMatch(term, vacConf, cvLookup);
            if (w > best) best = w;
            if (best >= 1.0) break;
        }
        return best;
    }

    public static bool IsMatched(
        string mhName,
        JsonElement vacancy,
        Dictionary<string, double> cvLookup,
        double threshold = ScoringConstants.SkillMatch.ExpansionThreshold)
        => Score(mhName, vacancy, cvLookup) >= threshold;

    private static double BestMatch(string vacTerm, double vacConf, Dictionary<string, double> cvLookup)
    {
        if (string.IsNullOrWhiteSpace(vacTerm)) return 0.0;
        if (cvLookup.TryGetValue(vacTerm, out var ec)) return vacConf * ec;

        var vacLower = vacTerm.ToLowerInvariant();
        double best = 0.0;
        foreach (var (cvTerm, cvConf) in cvLookup)
        {
            var cvLower = cvTerm.ToLowerInvariant();
            bool overlap = cvLower.Contains(vacLower, StringComparison.Ordinal)
                        || vacLower.Contains(cvLower, StringComparison.Ordinal);
            if (overlap)
            {
                double w = vacConf * cvConf;
                if (w > best) best = w;
                if (best >= 1.0) break;
            }
        }
        return best;
    }
}
