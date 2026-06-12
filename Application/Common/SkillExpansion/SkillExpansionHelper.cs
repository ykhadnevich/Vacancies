using System.Text;
using System.Text.Json;

namespace Application.Common.SkillExpansion;

/// <summary>
/// Shared helpers extracted from <c>GetAggregatedJobsV6Handler</c> so the recruiter
/// cabinet can build the same `_skills_expanded` / `_must_haves_expanded` JSON the
/// Mono prompt consumes. Pure functions, no state.
/// </summary>
public static class SkillExpansionHelper
{
    /// <summary>
    /// Inserts <paramref name="expansionJson"/> as a property named
    /// <paramref name="field"/> into <paramref name="baseJson"/>. If
    /// <paramref name="baseJson"/> already contains that property, it is replaced.
    /// Returns the original JSON unchanged when inputs cannot be parsed.
    /// </summary>
    public static string InjectExpansion(string baseJson, string field, string? expansionJson)
    {
        if (string.IsNullOrWhiteSpace(expansionJson)) return baseJson;
        try
        {
            using var baseDoc = JsonDocument.Parse(baseJson);
            if (baseDoc.RootElement.ValueKind != JsonValueKind.Object) return baseJson;

            using var expDoc = JsonDocument.Parse(expansionJson);

            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in baseDoc.RootElement.EnumerateObject())
                {
                    if (string.Equals(prop.Name, field, StringComparison.Ordinal)) continue;
                    prop.WriteTo(writer);
                }
                writer.WritePropertyName(field);
                expDoc.RootElement.WriteTo(writer);
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (JsonException)
        {
            return baseJson;
        }
    }

    /// <summary>
    /// Builds the `{ skill: [{term, confidence}, …] }` JSON the Mono prompt
    /// inspects for synonym hints. Skills with no vocab entry fall back to a
    /// single self-mapped term at confidence 1.0.
    /// </summary>
    public static string? BuildExpansionFromVocab(
        IReadOnlyList<string> skills,
        IReadOnlyDictionary<string, string> vocab)
    {
        if (skills.Count == 0) return null;

        var sb = new StringBuilder(skills.Count * 64);
        sb.Append('{');
        bool first = true;
        foreach (var skill in skills)
        {
            if (string.IsNullOrWhiteSpace(skill)) continue;
            if (!vocab.TryGetValue(skill, out var arrJson) || string.IsNullOrWhiteSpace(arrJson))
            {
                arrJson = "[{\"term\":" + JsonSerializer.Serialize(skill) + ",\"confidence\":1.0}]";
            }
            if (!first) sb.Append(',');
            sb.Append(JsonSerializer.Serialize(skill));
            sb.Append(':');
            sb.Append(arrJson);
            first = false;
        }
        sb.Append('}');
        return sb.ToString();
    }

    /// <summary>
    /// Extracts technical_skills + domain_skills + target_roles from a normalised CV summary JSON.
    /// </summary>
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
                if (!root.TryGetProperty(fieldName, out var arr)
                    || arr.ValueKind != JsonValueKind.Array) continue;
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

    /// <summary>
    /// Extracts must_have_skills + nice_to_have_skills from a normalised vacancy analysis JSON,
    /// plus the English-language role title hint if present (used to bias vocab resolution).
    /// </summary>
    public static (List<string> Skills, string? RoleHint) ExtractVacancySkillsAndRoleHint(
        string vacancyAnalysisJson)
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
                if (!root.TryGetProperty(fieldName, out var arr)
                    || arr.ValueKind != JsonValueKind.Array) continue;
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
