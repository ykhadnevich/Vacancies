using System.Text.RegularExpressions;

namespace Infrastructure.JobSources;


public static class UkrainianDateParser
{
    private static readonly Dictionary<string, int> UkrMonths = new(StringComparer.OrdinalIgnoreCase)
    {
        ["січня"]    = 1,  ["січень"]    = 1,
        ["лютого"]   = 2,  ["лютий"]     = 2,
        ["березня"]  = 3,  ["березень"]  = 3,
        ["квітня"]   = 4,  ["квітень"]   = 4,
        ["травня"]   = 5,  ["травень"]   = 5,
        ["червня"]   = 6,  ["червень"]   = 6,
        ["липня"]    = 7,  ["липень"]    = 7,
        ["серпня"]   = 8,  ["серпень"]   = 8,
        ["вересня"]  = 9,  ["вересень"]  = 9,
        ["жовтня"]   = 10, ["жовтень"]   = 10,
        ["листопада"]= 11, ["листопад"]  = 11,
        ["грудня"]   = 12, ["грудень"]   = 12,
    };


    private static readonly Regex DatePattern = new(
        @"(\d{1,2})\s+(січня|лютого|березня|квітня|травня|червня|липня|серпня|вересня|жовтня|листопада|грудня|січень|лютий|березень|квітень|травень|червень|липень|серпень|вересень|жовтень|листопад|грудень)(?:\s+(\d{4}))?",
        RegexOptions.IgnoreCase);


    public static DateTime? TryParse(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return null;

        var match = DatePattern.Match(text);
        if (!match.Success) return null;

        if (!int.TryParse(match.Groups[1].Value, out var day)) return null;
        if (!UkrMonths.TryGetValue(match.Groups[2].Value, out var month)) return null;

        var year = match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var y)
            ? y
            : DateTime.UtcNow.Year;

        try
        {
            var date = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Utc);

            if (date > DateTime.UtcNow.AddDays(1)) return null;
            if (date < DateTime.UtcNow.AddYears(-2)) return null;
            return date;
        }
        catch { return null; }
    }
}
