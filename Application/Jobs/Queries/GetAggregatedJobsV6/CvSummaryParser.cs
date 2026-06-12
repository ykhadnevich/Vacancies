using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Application.Jobs.Queries.GetAggregatedJobsV6;

public static class CvSummaryParser
{
    public static List<string> ExtractCvSkills(string cvSummaryJson)
    {
        var list = new List<string>();
        if (string.IsNullOrWhiteSpace(cvSummaryJson)) return list;
        try
        {
            using var doc = JsonDocument.Parse(cvSummaryJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return list;
            foreach (var fieldName in new[] { "technical_skills", "domain_skills", "target_roles" })
            {
                if (!root.TryGetProperty(fieldName, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s!);
                }
            }
        }
        catch (JsonException) { }
        return list;
    }

    public static (List<string> Skills, string? RoleHint) ExtractVacancySkillsAndRoleHint(string vacancyAnalysisJson)
    {
        var list = new List<string>();
        string? hint = null;
        if (string.IsNullOrWhiteSpace(vacancyAnalysisJson)) return (list, hint);
        try
        {
            using var doc = JsonDocument.Parse(vacancyAnalysisJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return (list, hint);
            foreach (var fieldName in new[] { "must_have_skills", "nice_to_have_skills" })
            {
                if (!root.TryGetProperty(fieldName, out var arr) || arr.ValueKind != JsonValueKind.Array) continue;
                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var s = item.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) list.Add(s!);
                }
            }
            if (root.TryGetProperty("role_title", out var roleObj)
                && roleObj.ValueKind == JsonValueKind.Object
                && roleObj.TryGetProperty("en", out var enEl)
                && enEl.ValueKind == JsonValueKind.String)
            {
                hint = enEl.GetString();
            }
        }
        catch (JsonException) { }
        return (list, hint);
    }
}
