using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Domain.Scoring;


public static class CvHasher
{
    public static string ComputeHash(string cvSummaryJson)
    {
        if (string.IsNullOrWhiteSpace(cvSummaryJson))
            throw new ArgumentException("CvSummaryJson cannot be empty", nameof(cvSummaryJson));

        string canonical;
        try
        {
            canonical = BuildCanonicalProjection(cvSummaryJson);
        }
        catch (JsonException)
        {
            canonical = "::unparseable::";
        }

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(canonical), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildCanonicalProjection(string cvSummaryJson)
    {
        using var doc = JsonDocument.Parse(cvSummaryJson);
        var root = doc.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            return "::not-object::";

        var sb = new StringBuilder(512);
        AppendSortedStringArray(sb, root, "technical_skills");
        sb.Append('|');
        AppendSortedStringArray(sb, root, "domain_skills");
        sb.Append('|');
        AppendSortedStringArray(sb, root, "target_roles");
        sb.Append('|');
        sb.Append(ReadStringOrEmpty(root, "seniority"));
        sb.Append('|');
        sb.Append(ReadStringOrEmpty(root, "english_level"));
        sb.Append('|');
        sb.Append(ReadExperienceMonths(root));
        return sb.ToString();
    }

    private static void AppendSortedStringArray(StringBuilder sb, JsonElement obj, string field)
    {
        if (!obj.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return;
        var items = new List<string>(arr.GetArrayLength());
        foreach (var el in arr.EnumerateArray())
            if (el.ValueKind == JsonValueKind.String)
            {
                var s = el.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                    items.Add(s.Trim().ToLowerInvariant());
            }
        items.Sort(StringComparer.Ordinal);
        sb.Append(string.Join(",", items));
    }

    private static string ReadStringOrEmpty(JsonElement obj, string field)
    {
        if (obj.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String)
            return (v.GetString() ?? string.Empty).Trim().ToLowerInvariant();
        return string.Empty;
    }

    private static int ReadExperienceMonths(JsonElement root)
    {
        if (!root.TryGetProperty("experience", out var exp)) return 0;
        if (exp.ValueKind == JsonValueKind.Number) return exp.GetInt32();

        if (exp.ValueKind == JsonValueKind.Array)
        {
            int total = 0;
            foreach (var pos in exp.EnumerateArray())
            {
                if (pos.ValueKind == JsonValueKind.Object
                    && pos.TryGetProperty("duration_months", out var dm)
                    && dm.ValueKind == JsonValueKind.Number)
                {
                    total += dm.GetInt32();
                }
            }
            return total;
        }

        if (exp.ValueKind == JsonValueKind.Object
            && exp.TryGetProperty("duration_months", out var topDm)
            && topDm.ValueKind == JsonValueKind.Number)
            return topDm.GetInt32();

        return 0;
    }
}
