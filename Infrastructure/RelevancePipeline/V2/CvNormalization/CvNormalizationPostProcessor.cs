using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public sealed class CvNormalizationPostProcessor : ICvNormalizationPostProcessor
{
    private readonly ILogger<CvNormalizationPostProcessor> _logger;

    public CvNormalizationPostProcessor(ILogger<CvNormalizationPostProcessor> logger)
    {
        _logger = logger;
    }


    public string Process(string rawJson, string cvRawText)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
            return rawJson;

        try
        {
            var node = JsonNode.Parse(rawJson);
            if (node is null) return rawJson;

            var domain     = ReadStringArray(node, "domain_skills");
            var technical  = ReadStringArray(node, "technical_skills");
            var unverified = ReadStringArray(node, "unverified_skills");


            domain     = domain.Select(StripTrailingParenthetical).ToList();
            technical  = technical.Select(StripTrailingParenthetical).ToList();
            unverified = unverified.Select(StripTrailingParenthetical).ToList();


            domain     = domain.Select(Canonicalize).ToList();
            technical  = technical.Select(Canonicalize).ToList();
            unverified = unverified.Select(Canonicalize).ToList();


            domain     = domain.Where(s => !IsRolePattern(s)).ToList();
            technical  = technical.Where(s => !IsRolePattern(s)).ToList();
            unverified = unverified.Where(s => !IsRolePattern(s)).ToList();


            var movedFromDomain    = domain.Where(IsSoftSkill).ToList();
            var movedFromTechnical = technical.Where(IsSoftSkill).ToList();
            domain.RemoveAll(IsSoftSkill);
            technical.RemoveAll(IsSoftSkill);
            unverified.AddRange(movedFromDomain);
            unverified.AddRange(movedFromTechnical);


            foreach (var stackChild in ParenStackChildren)
            {
                var inDomain = domain.FirstOrDefault(
                    s => string.Equals(s, stackChild, StringComparison.OrdinalIgnoreCase));
                if (inDomain is null) continue;
                if (AppearsOutsideParens(cvRawText, stackChild)) continue;

                domain.RemoveAll(s => string.Equals(s, stackChild, StringComparison.OrdinalIgnoreCase));
                if (!technical.Any(s => string.Equals(s, stackChild, StringComparison.OrdinalIgnoreCase)))
                    technical.Add(stackChild);
            }


            var domainSet = new HashSet<string>(domain, StringComparer.OrdinalIgnoreCase);
            technical  = technical.Where(s => !domainSet.Contains(s)).ToList();
            var techSet   = new HashSet<string>(technical, StringComparer.OrdinalIgnoreCase);
            unverified = unverified.Where(s => !domainSet.Contains(s) && !techSet.Contains(s)).ToList();


            var allLowercase = new HashSet<string>(
                domain.Concat(technical).Concat(unverified).Select(s => s.ToLowerInvariant()));

            foreach (var rescue in CaptureRescueCandidates)
            {
                if (allLowercase.Contains(rescue.ToLowerInvariant())) continue;
                if (!cvRawText.Contains(rescue, StringComparison.OrdinalIgnoreCase)) continue;

                domain.Add(rescue);
                allLowercase.Add(rescue.ToLowerInvariant());
            }


            domain     = StripHierarchicalChildren(domain);
            technical  = StripHierarchicalChildren(technical);


            domain     = DistinctPreservingOrder(domain);
            technical  = DistinctPreservingOrder(technical);
            unverified = DistinctPreservingOrder(unverified);


            node["domain_skills"]     = ToJsonArray(domain);
            node["technical_skills"]  = ToJsonArray(technical);
            node["unverified_skills"] = ToJsonArray(unverified);

            return node.ToJsonString(JsonSerializerOptions);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CvNormalizationPostProcessor: failed to post-process JSON — " +
                "returning Gemini output unchanged. Length={Len}", rawJson.Length);
            return rawJson;
        }
    }


    private static readonly JsonSerializerOptions JsonSerializerOptions = new()
    {
        WriteIndented = false
    };

    private static List<string> ReadStringArray(JsonNode root, string property)
    {
        if (root[property] is not JsonArray arr) return new List<string>();
        var result = new List<string>(arr.Count);
        foreach (var item in arr)
        {
            if (item is null) continue;
            var s = item.GetValue<string>();
            if (!string.IsNullOrWhiteSpace(s)) result.Add(s.Trim());
        }
        return result;
    }

    private static JsonArray ToJsonArray(IEnumerable<string> items)
    {
        var arr = new JsonArray();
        foreach (var s in items) arr.Add(JsonValue.Create(s));
        return arr;
    }


    private static List<string> StripHierarchicalChildren(List<string> items)
    {
        if (items.Count < 2) return items;

        var sortedByLen = items.OrderBy(s => s.Length).ToList();
        var keep = new List<string>();
        foreach (var candidate in items)
        {
            bool dropped = false;
            foreach (var parent in sortedByLen)
            {
                if (parent.Length >= candidate.Length) break;
                if (string.Equals(parent, candidate, StringComparison.OrdinalIgnoreCase)) continue;
                if (!candidate.StartsWith(parent + " ", StringComparison.OrdinalIgnoreCase)) continue;


                var suffix = candidate.Substring(parent.Length + 1);
                if (IsVersionSuffix(suffix)) continue;
                dropped = true;
                break;
            }
            if (!dropped) keep.Add(candidate);
        }
        return keep;
    }


    private static bool IsVersionSuffix(string suffix)
    {
        if (string.IsNullOrEmpty(suffix)) return false;
        foreach (var ch in suffix)
        {
            if (!char.IsDigit(ch) && ch != '.' && ch != 'x' && ch != 'X') return false;
        }
        return true;
    }

    private static List<string> DistinctPreservingOrder(IEnumerable<string> items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var s in items)
        {
            if (seen.Add(s)) result.Add(s);
        }
        return result;
    }


    private static readonly Dictionary<string, string> CanonicalForms = new(StringComparer.OrdinalIgnoreCase)
    {

        ["REST API understanding"]              = "REST API",
        ["RESTful API"]                         = "REST API",
        ["RESTful APIs"]                        = "REST API",
        ["SQL for data analysis"]               = "SQL",
        ["SQL databases"]                       = "SQL",
        ["NoSQL databases"]                     = "NoSQL",
        ["MS SQL Server"]                       = "SQL Server",

        ["Hypothesis formulation"]              = "Hypothesis validation",
        ["Hypothesis formulation & validation"] = "Hypothesis validation",
        ["Hypothesis testing"]                  = "Hypothesis validation",
        ["Mobile monetization strategies"]      = "Mobile monetization",
        ["MVP scope"]                           = "MVP scope definition",

        ["Agile"]                               = "Agile/Scrum",
        ["Scrum"]                               = "Agile/Scrum",
        ["Agile/Scrum methodology"]             = "Agile/Scrum",

        ["CI/CD workflows"]                     = "CI/CD",
        ["Continuous Integration"]              = "CI/CD",
        ["Continuous Deployment"]               = "CI/CD",

        ["AWS"]                                 = "AWS basics",

        ["JS"]                                  = "JavaScript",
        ["TS"]                                  = "TypeScript",
    };

    private static string Canonicalize(string s) =>
        CanonicalForms.TryGetValue(s.Trim(), out var c) ? c : s.Trim();


    private static readonly Regex TrailingParenthetical = new(
        @"\s+\([^)]*\)\s*$",
        RegexOptions.Compiled);


    private static string StripTrailingParenthetical(string s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s;
        var trimmed = s.Trim();
        var stripped = TrailingParenthetical.Replace(trimmed, string.Empty).Trim();


        return string.IsNullOrWhiteSpace(stripped) ? trimmed : stripped;
    }


    private static readonly Regex RolePattern = new(
        @"^\s*(?:Junior|Senior|Lead|Staff|Principal|Mid[- ]Level|Mobile|Backend|Frontend|Full[- ]?Stack|Data|ML|AI|DevOps|UX|UI|UX/UI)\s+" +
        @"(?:Product|Project|Marketing|Software|Mobile|Backend|Frontend|Full[- ]?Stack|Data|ML|AI|DevOps|UX|UI|Web|Cloud|Site Reliability|Security|QA|Test|Embedded|Firmware)?\s*" +
        @"(?:Manager|Engineer|Developer|Designer|Owner|Analyst|Scientist|Architect|Lead|Director)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BareRolePattern = new(
        @"^\s*(?:Product Manager|Product Owner|Project Manager|Software Engineer|" +
        @"Software Developer|Backend Developer|Frontend Developer|Full[- ]?Stack Developer|" +
        @"Data Engineer|Data Scientist|Data Analyst|ML Engineer|AI Engineer|" +
        @"DevOps Engineer|QA Engineer|UX Designer|UI Designer|UX/UI Designer|" +
        @"Mobile Product Manager|Growth Manager|Product Marketing Manager)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static bool IsRolePattern(string s) =>
        RolePattern.IsMatch(s) || BareRolePattern.IsMatch(s);


    private static readonly HashSet<string> SoftSkills = new(StringComparer.OrdinalIgnoreCase)
    {
        "Analytical thinking", "Critical thinking", "Strategic thinking",
        "Data-driven decision making", "Data-driven mindset", "Decision making",
        "Cross-functional collaboration", "Collaboration", "Teamwork",
        "Technical communication", "Communication", "Verbal communication",
        "Written communication", "Quick learner", "Fast learner", "Adaptable",
        "Adaptability", "Customer focus", "Customer-centric", "Customer empathy",
        "Attention to detail", "Problem solving", "Problem-solving",
        "Leadership", "Time management", "Self-starter", "Self-motivated",
        "Empathy", "Resilience", "Initiative",
    };

    private static bool IsSoftSkill(string s) =>
        SoftSkills.Contains(s.Trim());


    private static readonly string[] ParenStackChildren =
    {
        ".NET Core", "EF Core", "Entity Framework Core", "Entity Framework",
        "NumPy", "Pandas", "SciPy", "Matplotlib", "Seaborn",
        "Redux", "MobX", "RxJS", "Webpack", "Vite",
    };


    private static bool AppearsOutsideParens(string cvText, string item)
    {
        var withoutParens = Regex.Replace(cvText, @"\([^)]*\)", string.Empty);
        return withoutParens.Contains(item, StringComparison.OrdinalIgnoreCase);
    }


    private static readonly string[] CaptureRescueCandidates =
    {
        "Customer discovery",
        "Design patterns",
        "Application architecture",
        "Rapid iteration",
        "Roadmapping",
        "MVP methodology",
        "Business Model Canvas",
        "ICE framework",
        "Unit economics",
        "Financial modeling",
    };
}
