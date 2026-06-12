using System.Text.RegularExpressions;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public static class ReasonValidator
{
    public sealed record ValidationResult(
        bool IsValid,
        bool NeedsRegeneration,
        IReadOnlyList<string> HallucinatedGapsEn,
        IReadOnlyList<string> HallucinatedGapsUk,
        bool CalibrationDriftEn,
        bool CalibrationDriftUk,
        int WordCountEn,
        int WordCountUk,
        bool LengthOverflowEn,
        bool LengthOverflowUk);


    private static readonly string[] GapMarkersEn =
        { "Gaps:", "gaps:", "Missing:", "missing:" };
    private static readonly string[] GapMarkersUk =
        { "Брак:", "Прогалини:", "брак:", "прогалини:" };

    private const int MaxWords = 30;

    public static ValidationResult Validate(
        string reasonEn,
        string reasonUk,
        Verdict verdict,
        ScoringEvidence evidence)
    {
        var missingLower = evidence.MissingMustHaves
            .Select(s => s.ToLowerInvariant().Trim())
            .Where(s => s.Length > 0)
            .ToHashSet();
        var antiFlagLower = evidence.TriggeredAntiFlags
            .Select(s => s.ToLowerInvariant().Trim())
            .Where(s => s.Length > 0)
            .ToHashSet();

        var halucEn = FindHallucinatedGaps(reasonEn, GapMarkersEn, missingLower, antiFlagLower);
        var halucUk = FindHallucinatedGaps(reasonUk, GapMarkersUk, missingLower, antiFlagLower);

        var verdictEnExpected = verdict.ToEnglishText();
        var verdictUkExpected = verdict.ToUkrainianText();
        var driftEn = !string.IsNullOrWhiteSpace(reasonEn)
            && !reasonEn.TrimStart().StartsWith(verdictEnExpected, StringComparison.OrdinalIgnoreCase)
            && !ContainsContextLead(reasonEn, verdictEnExpected);
        var driftUk = !string.IsNullOrWhiteSpace(reasonUk)
            && !reasonUk.TrimStart().StartsWith(verdictUkExpected, StringComparison.OrdinalIgnoreCase)
            && !ContainsContextLead(reasonUk, verdictUkExpected);

        int wordsEn = WordCount(reasonEn);
        int wordsUk = WordCount(reasonUk);
        bool overflowEn = wordsEn > MaxWords;
        bool overflowUk = wordsUk > MaxWords;


        bool needsRegen = halucEn.Count > 0;
        bool isValid = !needsRegen && halucUk.Count == 0
                       && !driftEn && !driftUk && !overflowEn && !overflowUk;

        return new ValidationResult(
            IsValid: isValid,
            NeedsRegeneration: needsRegen,
            HallucinatedGapsEn: halucEn,
            HallucinatedGapsUk: halucUk,
            CalibrationDriftEn: driftEn,
            CalibrationDriftUk: driftUk,
            WordCountEn: wordsEn,
            WordCountUk: wordsUk,
            LengthOverflowEn: overflowEn,
            LengthOverflowUk: overflowUk);
    }


    public static (string en, string uk) Fixup(
        string reasonEn,
        string reasonUk,
        ValidationResult result,
        Verdict verdict)
    {
        var en = reasonEn;
        var uk = reasonUk;

        if (result.LengthOverflowEn)
            en = TruncateToWords(en, MaxWords);
        if (result.LengthOverflowUk)
            uk = TruncateToWords(uk, MaxWords);


        if (result.CalibrationDriftEn && !LooksLikeContextLead(en))
            en = $"{verdict.ToEnglishText()}. {en}";
        if (result.CalibrationDriftUk && !LooksLikeContextLead(uk))
            uk = $"{verdict.ToUkrainianText()}. {uk}";

        return (en, uk);
    }


    private static List<string> FindHallucinatedGaps(
        string text,
        string[] markers,
        HashSet<string> missingLower,
        HashSet<string> antiFlagLower)
    {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return result;

        var gapTokens = ExtractGapTokens(text, markers);
        foreach (var raw in gapTokens)
        {
            var token = raw.Trim().ToLowerInvariant();
            if (token.Length == 0) continue;

            token = Regex.Replace(token, @"^(missing|немає|нема|no)\s+", "");
            if (token.Length == 0) continue;


            bool matched = missingLower.Any(m => m.Contains(token) || token.Contains(m))
                        || antiFlagLower.Any(a => a.Contains(token) || token.Contains(a));
            if (!matched)
                result.Add(raw.Trim());
        }
        return result;
    }

    private static List<string> ExtractGapTokens(string text, string[] markers)
    {
        foreach (var marker in markers)
        {
            var idx = text.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (idx < 0) continue;
            var tail = text[(idx + marker.Length)..];


            var endIdx = tail.IndexOfAny(new[] { '.', '\n' });
            if (endIdx >= 0) tail = tail[..endIdx];

            return tail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim(' ', '.', ',', ';'))
                .Where(s => s.Length > 0)
                .ToList();
        }
        return new List<string>();
    }

    private static bool ContainsContextLead(string text, string verdictWord)
    {


        if (string.IsNullOrWhiteSpace(text)) return false;
        var head = text.Length > 100 ? text[..100] : text;
        return head.Contains(verdictWord, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeContextLead(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return false;


        var match = Regex.Match(text, @"^([^.]{1,80})\.\s+([A-ZА-ЯІЇЄ])");
        if (!match.Success) return false;
        var firstSentence = match.Groups[1].Value;
        var wordCount = firstSentence.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).Length;
        return wordCount <= 8;
    }

    private static int WordCount(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return 0;
        return text.Split(new[] { ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private static string TruncateToWords(string text, int maxWords)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (words.Length <= maxWords) return text;
        var truncated = string.Join(" ", words.Take(maxWords));

        if (!truncated.EndsWith('.')) truncated += ".";
        return truncated;
    }
}
