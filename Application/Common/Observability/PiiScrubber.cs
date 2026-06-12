using System.Text.RegularExpressions;

namespace Application.Common.Observability;

public static class PiiScrubber
{
    public const string EmailPlaceholder = "[email]";
    public const string PhonePlaceholder = "[phone]";

    private static readonly Regex EmailRegex = new(
        @"\b[\w._%+\-]+@[\w.\-]+\.[A-Za-z]{2,}\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    // The UA patterns anchor on the "38" or "0XX" prefix + a digit budget so plain
    // numeric tokens in stack traces / version strings are not matched.
    private static readonly Regex[] PhoneRegexes =
    [
        new Regex(
            @"\+?\s*38\s*0?\s*[(\-\s]?\s*\d{2,3}\s*[)\-\s]?\s*\d{3}\s*[\-\s]?\s*\d{2}\s*[\-\s]?\s*\d{2}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),

        new Regex(
            @"(?<!\d)\(?\s*0\d{2}\s*[)\-\s]?\s*\d{3}\s*[\-\s]?\s*\d{2}\s*[\-\s]?\s*\d{2}(?!\d)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),

        new Regex(
            @"\+\d{10,15}",
            RegexOptions.Compiled | RegexOptions.CultureInvariant),
    ];

    public static string Scrub(string? input)
    {
        if (string.IsNullOrEmpty(input))
            return input ?? string.Empty;
        var s = EmailRegex.Replace(input, EmailPlaceholder);
        foreach (var rx in PhoneRegexes)
            s = rx.Replace(s, PhonePlaceholder);
        return s;
    }
}
