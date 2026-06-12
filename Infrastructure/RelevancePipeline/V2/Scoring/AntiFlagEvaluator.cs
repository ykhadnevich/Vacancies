using System.Text.Json;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public static class AntiFlagEvaluator
{
    public sealed record Result(double Penalty, IReadOnlyList<string> Triggered);

    public static Result Evaluate(JsonElement cv, JsonElement vacancy)
    {
        var antiList = new List<string>();
        if (vacancy.TryGetProperty("anti_requirements", out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var a in arr.EnumerateArray())
                if (a.ValueKind == JsonValueKind.String)
                    antiList.Add(a.GetString() ?? "");
        }

        var triggered = new List<string>();

        foreach (var flag in antiList)
        {
            if (string.IsNullOrWhiteSpace(flag)) continue;
            var flagLow = flag.ToLowerInvariant();


            if (IsForeignLanguageFlag(flagLow, out var lang))
            {
                if (!CvHasLanguage(cv, lang)) triggered.Add(flag);
                continue;
            }


            if (flagLow.Contains("contract-only") || flagLow.Contains("volunteer") || flagLow.Contains("unpaid"))
            {
                triggered.Add(flag);
                continue;
            }


            if (flagLow.Contains("onsite only") || flagLow.Contains("must be") || flagLow.Contains("based in"))
            {
                triggered.Add(flag);
                continue;
            }


            // Previously this branch unconditionally added every unknown anti_requirement to `triggered`,
            // which meant any vacancy phrasing we hadn't explicitly handled was treated as a hard penalty
            // against the candidate regardless of relevance. Unknown flags now fall through without
            // penalty. If we ever need to surface them for review, add an Unknown collection here
            // and wire it through ScoringServiceV2 to the logger.
        }

        double penalty = triggered.Count switch
        {
            0 => ScoringConstants.AntiFlag.PenaltyNone,
            1 => ScoringConstants.AntiFlag.PenaltyOne,
            _ => ScoringConstants.AntiFlag.PenaltyMany,
        };
        return new Result(penalty, triggered);
    }

    private static bool IsForeignLanguageFlag(string flag, out string lang)
    {
        var foreign = new[] { "french", "german", "spanish", "italian", "polish", "dutch", "japanese", "chinese", "arabic" };
        foreach (var l in foreign)
            if (flag.Contains(l))
            {
                lang = l;
                return true;
            }
        lang = "";
        return false;
    }

    private static bool CvHasLanguage(JsonElement cv, string lang)
    {
        if (!cv.TryGetProperty("languages", out var langs) || langs.ValueKind != JsonValueKind.Array)
            return false;
        foreach (var l in langs.EnumerateArray())
        {
            if (l.ValueKind != JsonValueKind.Object) continue;
            if (l.TryGetProperty("language", out var n) && n.ValueKind == JsonValueKind.String)
            {
                var name = (n.GetString() ?? "").ToLowerInvariant();
                if (name.Contains(lang)) return true;
            }
        }
        return false;
    }
}
