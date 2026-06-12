using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Application.Common.Helpers;


public static class EvidenceCleaner
{


    private const string ImplicitPmMetricsCsv =
        "LTV|CAC|DAU|MAU|WAU|ROI|ROAS|NPS|CSAT|AOV|CVR|CTR|COGS|MRR|ARR|" +
        "KPI|KPIs|OKR|OKRs|" +
        "churn|churn rate|retention|engagement|funnel|conversion|" +
        "acquisition|activation|revenue|growth|" +
        "attribution|cohort|segmentation";

    private static readonly HashSet<string> ImplicitPmMetrics =
        new HashSet<string>(
            ImplicitPmMetricsCsv.Split('|'),
            StringComparer.OrdinalIgnoreCase);


    public static HashSet<string> BuildCvSkillSet(string? cvSummaryJson)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(cvSummaryJson)) return set;

        try
        {
            using var doc = JsonDocument.Parse(cvSummaryJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return set;

            foreach (var fieldName in new[] { "technical_skills", "domain_skills" })
            {
                if (!root.TryGetProperty(fieldName, out var arr)
                    || arr.ValueKind != JsonValueKind.Array) continue;

                foreach (var item in arr.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.String) continue;
                    var raw = item.GetString();
                    if (string.IsNullOrWhiteSpace(raw)) continue;
                    set.Add(raw!);

                    var parenIdx = raw!.IndexOf('(');
                    if (parenIdx > 0)
                    {
                        var bare = raw.Substring(0, parenIdx).Trim();
                        if (!string.IsNullOrWhiteSpace(bare)) set.Add(bare);
                    }
                }
            }
        }
        catch (JsonException)
        {

        }
        return set;
    }


    public static List<string> FilterMissing(IEnumerable<string> items, IReadOnlySet<string> blacklist)
        => items
            .Where(i => !string.IsNullOrWhiteSpace(i))
            .Select(i => i.Trim())
            .Where(i => !blacklist.Contains(i)
                     && !ImplicitPmMetrics.Contains(i))
            .ToList();
}
