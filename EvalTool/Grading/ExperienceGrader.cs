using System.Text.Json;

namespace EvalTool.Grading;


public sealed class ExperienceGrader
{
    public const int DurationToleranceMonths = 3;
    public const int YearsAgoToleranceYears = 2;

    public sealed record EntryRecord(string Title, string? Type, int? DurationMonths, int? YearsAgo);

    public sealed record ExperienceScores(
        double TitlesF1,
        double TypesAccuracy,
        double DurationsAccuracy,
        double YearsAgoAccuracy);

    public ExperienceScores Grade(JsonElement? actual, JsonElement? expected)
    {
        var actualEntries = ParseEntries(actual);
        var expectedEntries = ParseEntries(expected);


        string Norm(string t) => CompanyTitleNormalise(t);

        var actualTitles = actualEntries
            .Select(e => Norm(e.Title))
            .Where(t => !string.IsNullOrEmpty(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var expectedTitles = expectedEntries
            .Select(e => Norm(e.Title))
            .Where(t => !string.IsNullOrEmpty(t))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var titlesF1 = StringArrayF1Grader.F1Score(actualTitles, expectedTitles);


        var commonTitles = actualTitles
            .Intersect(expectedTitles, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (commonTitles.Count == 0)
            return new ExperienceScores(titlesF1, 0, 0, 0);

        int typeMatches = 0, durationMatches = 0, yearsAgoMatches = 0;
        foreach (var title in commonTitles)
        {
            var a = actualEntries.First(e => string.Equals(Norm(e.Title), title, StringComparison.OrdinalIgnoreCase));
            var e = expectedEntries.First(en => string.Equals(Norm(en.Title), title, StringComparison.OrdinalIgnoreCase));

            if (string.Equals(a.Type, e.Type, StringComparison.OrdinalIgnoreCase))
                typeMatches++;

            if (a.DurationMonths is not null && e.DurationMonths is not null &&
                Math.Abs(a.DurationMonths.Value - e.DurationMonths.Value) <= DurationToleranceMonths)
                durationMatches++;

            if (a.YearsAgo is not null && e.YearsAgo is not null &&
                Math.Abs(a.YearsAgo.Value - e.YearsAgo.Value) <= YearsAgoToleranceYears)
                yearsAgoMatches++;
        }

        var total = (double)commonTitles.Count;
        return new ExperienceScores(
            TitlesF1: titlesF1,
            TypesAccuracy: typeMatches / total,
            DurationsAccuracy: durationMatches / total,
            YearsAgoAccuracy: yearsAgoMatches / total);
    }


    private static readonly string[] CompanySuffixes =
    {
        " inc.", " inc",
        " ltd.", " ltd",
        " llc",
        " co.", " co",
        " corp.", " corp", " corporation",
        " group",
        " holdings", " holding",
        " investments", " investment",
        " solutions", " solution",
        " ventures", " venture",
        " software",
        " ag", " gmbh", " s.a.", " sa", " plc"
    };


    private static string CompanyTitleNormalise(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "";
        var t = title.Trim().ToLowerInvariant();


        foreach (var suf in CompanySuffixes.OrderByDescending(s => s.Length))
        {
            if (t.EndsWith(suf))
            {
                t = t.Substring(0, t.Length - suf.Length).Trim();
                break;
            }
        }


        while (t.Contains("  ")) t = t.Replace("  ", " ");
        return t.Trim();
    }

    private static List<EntryRecord> ParseEntries(JsonElement? element)
    {
        var list = new List<EntryRecord>();
        if (element is null || element.Value.ValueKind != JsonValueKind.Array) return list;

        foreach (var item in element.Value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object) continue;

            string title = item.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                ? t.GetString()?.Trim() ?? "" : "";
            string? type = item.TryGetProperty("type", out var ty) && ty.ValueKind == JsonValueKind.String
                ? ty.GetString()?.Trim() : null;
            int? duration = item.TryGetProperty("duration_months", out var d) && d.ValueKind == JsonValueKind.Number
                ? d.GetInt32() : null;
            int? yearsAgo = item.TryGetProperty("years_ago", out var y) && y.ValueKind == JsonValueKind.Number
                ? y.GetInt32() : null;

            list.Add(new EntryRecord(title, type, duration, yearsAgo));
        }
        return list;
    }
}
