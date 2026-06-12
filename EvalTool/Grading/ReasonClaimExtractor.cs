using System.Text.RegularExpressions;

namespace EvalTool.Grading;


public sealed class ReasonClaimExtractor
{


    private static readonly string[] VerdictWords =
        { "Strong match", "Partial match", "Weak match", "Mismatch", "Strong", "Partial", "Weak" };

    public List<string> Extract(string reasonEn)
    {
        var claims = new List<string>();
        if (string.IsNullOrWhiteSpace(reasonEn)) return claims;

        var text = reasonEn.Trim();


        var contextLead = ExtractContextLead(text);
        if (!string.IsNullOrWhiteSpace(contextLead))
            claims.Add(contextLead);


        foreach (var s in ExtractListAfter(text, "Strengths:"))
            claims.Add($"Candidate has {s} expertise.");


        var (gaps, antiFlags) = ExtractGapsAndAntiFlags(text);
        foreach (var g in gaps)
            claims.Add($"Vacancy requires {g} but candidate lacks it.");
        foreach (var af in antiFlags)
            claims.Add($"This vacancy has the constraint: {af}.");

        return claims;
    }

    private static string ExtractContextLead(string text)
    {


        int earliestVerdictIdx = -1;
        foreach (var verdict in VerdictWords)
        {
            var idx = text.IndexOf(verdict, StringComparison.OrdinalIgnoreCase);
            if (idx >= 0 && (earliestVerdictIdx == -1 || idx < earliestVerdictIdx))
                earliestVerdictIdx = idx;
        }
        if (earliestVerdictIdx <= 0) return string.Empty;

        var lead = text[..earliestVerdictIdx].TrimEnd(' ', '.', ';', ',').Trim();


        var words = lead.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length is 0 or > 12) return string.Empty;

        return lead.EndsWith('.') ? lead : lead + ".";
    }

    private static IEnumerable<string> ExtractListAfter(string text, string marker)
    {
        var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) yield break;

        var tail = text[(idx + marker.Length)..];


        var terminators = new[] { '.', '\n' };
        var endIdx = tail.IndexOfAny(terminators);
        if (endIdx >= 0) tail = tail[..endIdx];


        var semiIdx = tail.IndexOf(';');
        if (semiIdx >= 0) tail = tail[..semiIdx];

        foreach (var raw in tail.Split(','))
        {
            var item = raw.Trim(' ', '.', ',', ';');
            if (!string.IsNullOrWhiteSpace(item))
                yield return CleanLeadingFiller(item);
        }
    }

    private static (List<string> gaps, List<string> antiFlags) ExtractGapsAndAntiFlags(string text)
    {
        var gaps = new List<string>();
        var antiFlags = new List<string>();

        var idx = text.IndexOf("Gaps:", StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return (gaps, antiFlags);

        var tail = text[(idx + "Gaps:".Length)..];


        var endIdx = tail.IndexOfAny(new[] { '.', '\n' });
        if (endIdx >= 0) tail = tail[..endIdx];


        var semiIdx = tail.IndexOf(';');
        string skillsPart, antiFlagsPart;
        if (semiIdx >= 0)
        {
            skillsPart = tail[..semiIdx];
            antiFlagsPart = tail[(semiIdx + 1)..];
        }
        else
        {
            skillsPart = tail;
            antiFlagsPart = string.Empty;
        }

        foreach (var raw in skillsPart.Split(','))
        {
            var item = raw.Trim(' ', '.', ',', ';');
            if (!string.IsNullOrWhiteSpace(item))
                gaps.Add(CleanLeadingFiller(item));
        }

        foreach (var raw in antiFlagsPart.Split(','))
        {
            var item = raw.Trim(' ', '.', ',', ';');
            if (!string.IsNullOrWhiteSpace(item))
                antiFlags.Add(item);
        }

        return (gaps, antiFlags);
    }


    private static string CleanLeadingFiller(string item)
    {
        return Regex.Replace(item, @"^(missing|no)\s+", "", RegexOptions.IgnoreCase).Trim();
    }
}
