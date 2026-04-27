using HtmlAgilityPack;
using System.Net;
using System.Text.RegularExpressions;

namespace Infrastructure.Helpers;

public static class HtmlHelper
{
    private static readonly string[] LinkedInNoise =
    {
        "Join or sign in to find your next job",
        "New to LinkedIn?",
        "Join now",
        "Email or phone",
        "Password",
        "Forgot password?",
        "Sign in",
        "Sign in with Email",
        "Show more",
        "Show less",
        "Similar jobs",
        "Show more jobs like this",
        "Show fewer jobs like this",
        "People also viewed",
        "See who you know",
        "See who",
        "has hired for this role",
        "Report this job",
        "Use AI to assess",
        "Am I a good fit",
        "Get AI-powered advice",
        "Get notified when a new job is posted",
        "Set alert",
        "Be among the first",
        "applicants",
        "Apply",
        "Save",
        " ago",
    };

    public static string? ExtractLinkedInDescription(string? rawText)
    {
        if (string.IsNullOrEmpty(rawText)) return null;

        var startMarkers = new[]
        {
            "\nDescription\n",
            "\nAbout the job\n",
            "\nAbout this role\n",
            "\nAbout the Role\n",
            "\nJob Description\n",
            "\nAbout Us\n",
            "\nAbout the company\n",
        };

        var endMarkers = new[]
        {
            "\nSimilar jobs\n",
            "\nPeople also viewed\n",
            "\nShow more jobs",
            "\nSee who you know",
            "\nGet notified",
            "\nSign in to set job alerts",
        };

        var text = rawText;

        foreach (var marker in startMarkers)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                text = text[(idx + marker.Length)..];
                break;
            }
        }

        foreach (var marker in endMarkers)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0)
            {
                text = text[..idx];
                break;
            }
        }

        var lines = text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => l.Length > 3)
            .Where(l => !LinkedInNoise.Any(n =>
                l.Equals(n, StringComparison.OrdinalIgnoreCase) ||
                l.Contains(n, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var result = string.Join("\n", lines).Trim();
        result = Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }

    public static string? StripHtml(string? html)
    {
        if (string.IsNullOrEmpty(html)) return null;

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var nodesToRemove = doc.DocumentNode
            .SelectNodes("//script|//style|//noscript") ?? Enumerable.Empty<HtmlNode>();
        foreach (var node in nodesToRemove.ToList())
            node.Remove();

        foreach (var node in doc.DocumentNode
            .SelectNodes("//br|//p|//li|//div|//h1|//h2|//h3|//h4")
            ?? Enumerable.Empty<HtmlNode>())
        {
            node.ParentNode?.ReplaceChild(
                doc.CreateTextNode("\n" + node.InnerText), node);
        }

        var text = doc.DocumentNode.InnerText;
        text = WebUtility.HtmlDecode(text);

        var lines = text
            .Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Where(l => l.Length > 2)
            .Where(l => !LinkedInNoise.Any(noise =>
                l.Equals(noise, StringComparison.OrdinalIgnoreCase) ||
                l.StartsWith(noise, StringComparison.OrdinalIgnoreCase)))
            .ToList();

        var result = string.Join("\n", lines);
        result = Regex.Replace(result, @"\n{3,}", "\n\n").Trim();
        return string.IsNullOrWhiteSpace(result) ? null : result;
    }
}