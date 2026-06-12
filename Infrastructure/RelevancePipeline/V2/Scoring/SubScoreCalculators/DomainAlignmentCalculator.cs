using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;


public sealed class DomainAlignmentCalculator : ISubScoreCalculator
{
    public SubScoreAxis Axis => SubScoreAxis.DomainAlignment;


    // Floors live in Domain/Scoring/ScoringConstants.DomainFloors — see that
    // class for the rationale behind each family bucket.

    public double Compute(JsonElement cv, JsonElement vacancy)
    {
        var family = RoleFamilyDetector.Detect(cv);

        string domainEn = "";
        if (vacancy.TryGetProperty("domain_context", out var dc) && dc.ValueKind == JsonValueKind.Object
            && dc.TryGetProperty("en", out var en) && en.ValueKind == JsonValueKind.String)
        {
            domainEn = en.GetString() ?? "";
        }
        if (string.IsNullOrWhiteSpace(domainEn) || domainEn.Equals("other", StringComparison.OrdinalIgnoreCase))
            return FloorForEmpty(family);

        var domainWords = Tokenize(domainEn).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var cvDomains = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (cv.TryGetProperty("domain_skills", out var ds) && ds.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in ds.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.String)
                {
                    foreach (var t in Tokenize(item.GetString() ?? ""))
                        cvDomains.Add(t);
                }
            }
        }
        if (domainWords.Count == 0) return FloorForEmpty(family);

        int overlap = domainWords.Intersect(cvDomains, StringComparer.OrdinalIgnoreCase).Count();
        double ratio = (double)overlap / domainWords.Count;

        double floor = FloorForMatch(family);
        return Math.Min(1.0, floor + (1.0 - floor) * ratio);
    }


    private static double FloorForEmpty(RoleFamily family) => family switch
    {
        RoleFamily.Engineering or RoleFamily.DevOps or RoleFamily.Data
            => ScoringConstants.DomainFloors.EmptyTech,
        RoleFamily.ProductManagement
            or RoleFamily.Design
            or RoleFamily.Marketing
            => ScoringConstants.DomainFloors.EmptyDomainHeavy,
        _ => ScoringConstants.DomainFloors.EmptyDefault,
    };

    private static double FloorForMatch(RoleFamily family) => family switch
    {
        RoleFamily.Engineering or RoleFamily.DevOps or RoleFamily.Data
            => ScoringConstants.DomainFloors.MatchTech,
        RoleFamily.ProductManagement
            or RoleFamily.Design
            or RoleFamily.Marketing
            => ScoringConstants.DomainFloors.MatchDomainHeavy,
        _ => ScoringConstants.DomainFloors.MatchDefault,
    };

    private static IEnumerable<string> Tokenize(string s)
    {
        var lower = s.ToLowerInvariant();
        var sb = new System.Text.StringBuilder();
        foreach (var c in lower)
            sb.Append(char.IsLetterOrDigit(c) || c == '.' || c == '#' || c == '+' ? c : ' ');
        return sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)
                 .Where(t => t.Length >= 2);
    }
}
