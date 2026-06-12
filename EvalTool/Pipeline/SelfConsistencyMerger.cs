using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace EvalTool.Pipeline;


public sealed class SelfConsistencyMerger
{


    public string Merge(IReadOnlyList<string> runs)
    {
        if (runs.Count == 0) throw new ArgumentException("runs must contain at least one JSON.");
        if (runs.Count == 1) return runs[0];

        var nodes = runs.Select(r => JsonNode.Parse(r)!).ToList();
        var threshold = (runs.Count / 2) + 1;

        var result = new JsonObject();

        result["seniority"]                   = MajorityString(nodes, "seniority");
        result["target_roles"]                = MajorityStringArray(nodes, "target_roles", threshold);
        result["domain_skills"]               = MajorityStringArray(nodes, "domain_skills", threshold);
        result["technical_skills"]            = MajorityStringArray(nodes, "technical_skills", threshold);
        result["unverified_skills"]           = MajorityStringArray(nodes, "unverified_skills", threshold);
        result["experience"]                  = MergeExperience(nodes, threshold);
        result["education"]                   = MergeEducation(nodes);
        result["english_level"]               = MajorityString(nodes, "english_level");
        result["languages"]                   = MergeLanguages(nodes, threshold);
        result["has_real_product_experience"] = MajorityBool(nodes, "has_real_product_experience");
        result["career_switcher"]             = MajorityBool(nodes, "career_switcher");

        return result.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }


    private static JsonNode? MajorityString(List<JsonNode> nodes, string field)
    {
        var values = nodes
            .Select(n => n[field]?.GetValue<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (values.Count == 0) return null;
        var winner = values
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First().First();
        return JsonValue.Create(winner);
    }

    private static JsonNode? MajorityBool(List<JsonNode> nodes, string field)
    {
        int t = 0, f = 0;
        foreach (var n in nodes)
        {
            var v = n[field];
            if (v is null) continue;
            try
            {
                if (v.GetValue<bool>()) t++; else f++;
            }
            catch {  }
        }
        if (t == 0 && f == 0) return null;
        return JsonValue.Create(t >= f);
    }


    private static JsonArray MajorityStringArray(List<JsonNode> nodes, string field, int threshold)
    {
        var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var firstSeenCasing = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var firstSeenOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int orderCounter = 0;

        foreach (var n in nodes)
        {
            if (n[field] is not JsonArray arr) continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in arr)
            {
                var s = item?.GetValue<string>();
                if (string.IsNullOrWhiteSpace(s)) continue;
                if (!seen.Add(s)) continue;
                counts[s] = counts.GetValueOrDefault(s, 0) + 1;
                if (!firstSeenCasing.ContainsKey(s)) firstSeenCasing[s] = s;
                if (!firstSeenOrder.ContainsKey(s)) firstSeenOrder[s] = orderCounter++;
            }
        }

        var winners = counts
            .Where(kv => kv.Value >= threshold)
            .OrderBy(kv => firstSeenOrder[kv.Key])
            .Select(kv => firstSeenCasing[kv.Key])
            .ToList();

        var result = new JsonArray();
        foreach (var w in winners) result.Add(w);
        return result;
    }


    private static JsonArray MergeLanguages(List<JsonNode> nodes, int threshold)
    {


        var langVotes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var langLevels = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var langOrder = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int orderCounter = 0;

        foreach (var n in nodes)
        {
            if (n["languages"] is not JsonArray arr) continue;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in arr)
            {
                if (item is not JsonObject obj) continue;
                var lang = obj["language"]?.GetValue<string>()?.Trim();
                var level = obj["level"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(lang) || string.IsNullOrEmpty(level)) continue;
                if (!seen.Add(lang)) continue;
                langVotes[lang] = langVotes.GetValueOrDefault(lang, 0) + 1;
                if (!langLevels.ContainsKey(lang)) langLevels[lang] = new List<string>();
                langLevels[lang].Add(level);
                if (!langOrder.ContainsKey(lang)) langOrder[lang] = orderCounter++;
            }
        }

        var result = new JsonArray();
        foreach (var kv in langVotes.OrderBy(kv => langOrder[kv.Key]))
        {
            if (kv.Value < threshold) continue;
            var levelWinner = langLevels[kv.Key]
                .GroupBy(l => l, StringComparer.OrdinalIgnoreCase)
                .OrderByDescending(g => g.Count())
                .First().First();
            result.Add(new JsonObject
            {
                ["language"] = kv.Key,
                ["level"] = levelWinner
            });
        }
        return result;
    }


    private static JsonArray MergeExperience(List<JsonNode> nodes, int threshold)
    {


        var buckets = new Dictionary<string, List<JsonObject>>(StringComparer.OrdinalIgnoreCase);
        var order = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        int orderCounter = 0;

        foreach (var n in nodes)
        {
            if (n["experience"] is not JsonArray arr) continue;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var item in arr)
            {
                if (item is not JsonObject obj) continue;
                var title = obj["title"]?.GetValue<string>()?.Trim();
                if (string.IsNullOrEmpty(title)) continue;
                if (!seen.Add(title)) continue;
                if (!buckets.ContainsKey(title))
                {
                    buckets[title] = new List<JsonObject>();
                    order[title] = orderCounter++;
                }
                buckets[title].Add(obj);
            }
        }

        var result = new JsonArray();
        foreach (var kv in buckets.OrderBy(kv => order[kv.Key]))
        {
            if (kv.Value.Count < threshold) continue;
            var merged = new JsonObject
            {
                ["title"]             = kv.Key,
                ["type"]              = MajorityFromObjects(kv.Value, "type"),
                ["duration_months"]   = MedianInt(kv.Value, "duration_months"),
                ["years_ago"]         = MedianInt(kv.Value, "years_ago")
            };
            result.Add(merged);
        }
        return result;
    }


    private static JsonObject MergeEducation(List<JsonNode> nodes)
    {
        var educations = nodes
            .Select(n => n["education"] as JsonObject)
            .Where(o => o is not null)
            .Cast<JsonObject>()
            .ToList();
        if (educations.Count == 0) return new JsonObject();

        return new JsonObject
        {
            ["degree"]          = MajorityFromObjects(educations, "degree"),
            ["field"]           = MajorityFromObjects(educations, "field"),
            ["is_relevant"]     = MajorityBoolFromObjects(educations, "is_relevant"),
            ["status"]          = MajorityFromObjects(educations, "status"),
            ["current_year"]    = MedianInt(educations, "current_year"),
            ["graduation_year"] = MedianInt(educations, "graduation_year")
        };
    }


    private static JsonNode? MajorityFromObjects(List<JsonObject> objs, string field)
    {
        var values = objs
            .Select(o => o[field]?.GetValue<string>())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();
        if (values.Count == 0) return null;
        var winner = values
            .GroupBy(s => s, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First().First();
        return JsonValue.Create(winner);
    }

    private static JsonNode? MajorityBoolFromObjects(List<JsonObject> objs, string field)
    {
        int t = 0, f = 0;
        foreach (var o in objs)
        {
            var v = o[field];
            if (v is null) continue;
            try { if (v.GetValue<bool>()) t++; else f++; } catch { }
        }
        if (t == 0 && f == 0) return null;
        return JsonValue.Create(t >= f);
    }

    private static JsonNode? MedianInt(List<JsonObject> objs, string field)
    {
        var ints = new List<int>();
        foreach (var o in objs)
        {
            var v = o[field];
            if (v is null) continue;
            try { ints.Add(v.GetValue<int>()); } catch { }
        }
        if (ints.Count == 0) return null;
        ints.Sort();
        int mid = ints.Count / 2;
        int median = (ints.Count % 2 == 1)
            ? ints[mid]
            : (ints[mid - 1] + ints[mid]) / 2;
        return JsonValue.Create(median);
    }
}
