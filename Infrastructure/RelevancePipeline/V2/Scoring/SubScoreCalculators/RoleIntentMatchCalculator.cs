using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class RoleIntentMatchCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.RoleIntentMatch;

    private static readonly HashSet<string> SeniorityTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "junior", "jr", "middle", "mid", "senior", "sr", "lead", "principal",
        "staff", "intern", "trainee", "strong", "head", "chief",
        "молодший", "старший", "провідний", "стажер"
    };

    private static readonly HashSet<string> SuffixTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "engineer", "інженер", "developer", "розробник",
        "specialist", "спеціаліст"
    };


    // FallbackWeight (the multiplier applied to revealed-intent matches) lives in
    // Domain/Scoring/ScoringConstants.RoleIntent.FallbackWeight.

    public double Compute(JsonElement cv, JsonElement vacancy)
    {

        string roleTitle = "";
        if (vacancy.TryGetProperty("role_title", out var rt) && rt.ValueKind == JsonValueKind.Object
            && rt.TryGetProperty("en", out var rtEn) && rtEn.ValueKind == JsonValueKind.String)
        {
            roleTitle = rtEn.GetString() ?? "";
        }
        var vacancyTokens = Normalize(roleTitle);
        if (vacancyTokens.Count == 0) return ScoringConstants.RoleIntent.ScoreEmptyTitle;


        // Stated intent: explicit target_roles[] populated by the user / CV normalization.
        double primaryJaccard = BestJaccardAgainstTargetRoles(cv, vacancyTokens);
        double primaryScore = ScoreFromJaccard(primaryJaccard);


        // Revealed intent: job titles from the candidate's experience history.
        // Weaker signal than stated intent — FallbackWeight reflects relative strength.
        // We compute both and take the max so a strong revealed signal isn't overshadowed
        // by a weak stated one (e.g. CV with stale `target_roles` but consistent recent history).
        double fallbackJaccard = BestJaccardAgainstExperienceTitles(cv, vacancyTokens);
        double fallbackScore = ScoreFromJaccard(fallbackJaccard)
                             * ScoringConstants.RoleIntent.FallbackWeight;

        return Math.Max(primaryScore, fallbackScore);
    }

    private static double BestJaccardAgainstTargetRoles(JsonElement cv, HashSet<string> vacancyTokens)
    {
        double best = 0;
        if (!cv.TryGetProperty("target_roles", out var tr) || tr.ValueKind != JsonValueKind.Array)
            return best;

        foreach (var t in tr.EnumerateArray())
        {
            if (t.ValueKind != JsonValueKind.String) continue;
            var cvTokens = Normalize(t.GetString() ?? "");
            if (cvTokens.Count == 0) continue;
            double j = Jaccard(vacancyTokens, cvTokens);
            if (j > best) best = j;
        }
        return best;
    }

    private static double BestJaccardAgainstExperienceTitles(JsonElement cv, HashSet<string> vacancyTokens)
    {
        double best = 0;
        if (!cv.TryGetProperty("experience", out var exp) || exp.ValueKind != JsonValueKind.Array)
            return best;

        foreach (var item in exp.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            foreach (var field in new[] { "role", "title", "position" })
            {
                if (!item.TryGetProperty(field, out var v) || v.ValueKind != JsonValueKind.String) continue;
                var cvTokens = Normalize(v.GetString() ?? "");
                if (cvTokens.Count == 0) continue;
                double j = Jaccard(vacancyTokens, cvTokens);
                if (j > best) best = j;
            }
        }
        return best;
    }

    private static double ScoreFromJaccard(double j) => j switch
    {
        >= ScoringConstants.RoleIntent.JaccardHigh => ScoringConstants.RoleIntent.ScoreHigh,
        >= ScoringConstants.RoleIntent.JaccardMid  => ScoringConstants.RoleIntent.ScoreMid,
        > 0                                        => ScoringConstants.RoleIntent.ScoreLow,
        _                                          => ScoringConstants.RoleIntent.ScoreNone,
    };

    private static HashSet<string> Normalize(string s)
    {
        var lower = s.ToLowerInvariant();
        var sb = new System.Text.StringBuilder(lower.Length);
        foreach (var c in lower)
            sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '#' || c == '+' ? c : ' ');
        var parts = sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var p in parts)
        {
            if (p.Length < 2) continue;
            if (SeniorityTokens.Contains(p)) continue;
            if (SuffixTokens.Contains(p)) continue;
            set.Add(p);
        }
        return set;
    }

    private static double Jaccard(HashSet<string> a, HashSet<string> b)
    {
        if (a.Count == 0 || b.Count == 0) return 0;
        int intersect = a.Intersect(b, StringComparer.OrdinalIgnoreCase).Count();
        int union = a.Union(b, StringComparer.OrdinalIgnoreCase).Count();
        return union == 0 ? 0 : (double)intersect / union;
    }
}
