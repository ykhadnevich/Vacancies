using System.Text.RegularExpressions;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public static class SkillCanonicalizer
{


    public static bool Matches(string needed, HashSet<string> haveExpanded)
    {
        foreach (var v in ExpandOne(needed))
            if (haveExpanded.Contains(v)) return true;
        return false;
    }


    public static HashSet<string> ExpandAll(IEnumerable<string> skills)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in skills)
            foreach (var v in ExpandOne(s))
                set.Add(v);
        return set;
    }


    public static IEnumerable<string> ExpandOne(string raw)
    {
        var s = raw?.Trim() ?? "";
        if (string.IsNullOrEmpty(s)) yield break;
        yield return s;

        var stripped = StripVersion(s);
        bool changed = !string.Equals(stripped, s, StringComparison.OrdinalIgnoreCase);
        if (changed) yield return stripped;

        if (Aliases.TryGetValue(s, out var canon))
            yield return canon;
        if (changed && Aliases.TryGetValue(stripped, out var canonStripped))
            yield return canonStripped;
    }


    public static string StripVersion(string s)
    {
        var m = VersionRegex.Match(s);
        return m.Success ? m.Groups[1].Value.Trim() : s;
    }

    private static readonly Regex VersionRegex =
        new(@"^(.+?)\s+\d+(\.\d+)*$", RegexOptions.Compiled);


    private static readonly Dictionary<string, string> Aliases = new(StringComparer.OrdinalIgnoreCase)
    {

        ["asp.net"]               = ".net",
        ["asp.net core"]          = ".net",
        ["asp.net mvc"]           = ".net",
        ["asp.net web api"]       = ".net",
        [".net core"]             = ".net",
        [".net framework"]        = ".net",


        ["ef core"]               = "entity framework",
        ["entity framework core"] = "entity framework",


        ["google cloud"]          = "gcp",
        ["google cloud platform"] = "gcp",
        ["amazon web services"]   = "aws",
        ["microsoft azure"]       = "azure",


        ["k8s"]                   = "kubernetes",


        ["reactjs"]               = "react",
        ["react.js"]              = "react",
        ["nextjs"]                = "next.js",
        ["nodejs"]                = "node.js",
        ["node"]                  = "node.js",
        ["vuejs"]                 = "vue",
        ["vue.js"]                = "vue",


        ["postgres"]              = "postgresql",
        ["mssql"]                 = "sql server",
        ["microsoft sql server"]  = "sql server",
        ["mongo"]                 = "mongodb",


        ["pytorch lightning"]     = "pytorch",
        ["tf"]                    = "tensorflow",


        ["swiftui"]               = "swift",
        ["jetpack compose"]       = "kotlin",


        ["pm"]                                = "product management",
        ["product manager"]                   = "product management",
        ["product owner"]                     = "product management",
        ["produkt manager"]                   = "product management",
        ["продакт-менеджер"]                  = "product management",
        ["продакт"]                           = "product management",


        ["a/b test"]                          = "a/b testing",
        ["a/b tests"]                         = "a/b testing",
        ["ab testing"]                        = "a/b testing",
        ["split testing"]                     = "a/b testing",
        ["customer research"]                 = "customer discovery",
        ["user research"]                     = "customer discovery",
        ["user interviews"]                   = "customer discovery",
        ["customer development"]              = "customer discovery",
        ["hypothesis testing"]                = "hypothesis validation",
        ["product discovery"]                 = "customer discovery",
        ["competitor analysis"]               = "market analysis",
        ["competitive analysis"]              = "market analysis",
        ["market research"]                   = "market analysis",
        ["go-to-market"]                      = "gtm",
        ["go to market"]                      = "gtm",
        ["okrs"]                              = "okr",
        ["kpis"]                              = "kpi",
        ["roadmap"]                           = "roadmap planning",
        ["product roadmap"]                   = "roadmap planning",
        ["product strategy"]                  = "product strategy",
        ["product positioning"]               = "positioning",
        ["messaging strategy"]                = "messaging",
        ["product marketing"]                 = "product marketing",
        ["ice"]                               = "ice prioritization",
        ["ice framework"]                     = "ice prioritization",
        ["rice"]                              = "rice prioritization",
        ["rice framework"]                    = "rice prioritization",
        ["jtbd"]                              = "jobs to be done",
        ["jobs-to-be-done"]                   = "jobs to be done",


        ["google analytics"]                  = "ga4",
        ["google analytics 4"]                = "ga4",
        ["mixpanel"]                          = "mixpanel",
        ["amplitude"]                         = "amplitude",
        ["heap"]                              = "heap analytics",
        ["hotjar"]                            = "hotjar",


        ["atlassian jira"]                    = "jira",
        ["jira software"]                     = "jira",
        ["asana"]                             = "asana",
        ["trello"]                            = "trello",
        ["notion"]                            = "notion",
        ["confluence"]                        = "confluence",
        ["miro"]                              = "miro",
        ["figma"]                             = "figma",
        ["productboard"]                      = "productboard",


        ["microsoft excel"]                   = "excel",
        ["ms excel"]                          = "excel",
        ["google sheets"]                     = "google sheets",
        ["spreadsheets"]                      = "google sheets",
        ["tables"]                            = "google sheets",
        ["powerpoint"]                        = "powerpoint",
        ["ms powerpoint"]                     = "powerpoint",
        ["google docs"]                       = "docs",
        ["microsoft office"]                  = "ms office",


        ["unit-economics"]                    = "unit economics",
        ["unit economy"]                      = "unit economics",
        ["юніт економіка"]                    = "unit economics",
        ["юніт-економіка"]                    = "unit economics",
        ["cac"]                               = "customer acquisition cost",
        ["ltv"]                               = "lifetime value",
        ["aov"]                               = "average order value",
        ["arpu"]                              = "average revenue per user",


        ["b2b sales"]                         = "sales",
        ["b2c sales"]                         = "sales",
        ["lead generation"]                   = "lead gen",
        ["negotiations"]                      = "negotiation",


        ["roadmapping"]                       = "roadmap planning",
        ["product roadmap"]                   = "roadmap planning",
        ["roadmap"]                           = "roadmap planning",
        ["product planning"]                  = "roadmap planning",
        ["product strategy planning"]         = "roadmap planning",


        ["hypothesis testing"]                = "hypothesis validation",
        ["testing hypotheses"]                = "hypothesis validation",
        ["experimentation"]                   = "hypothesis validation",
        ["experiment design"]                 = "hypothesis validation",
        ["продуктові гіпотези"]               = "hypothesis validation",


        ["mvp"]                               = "mvp methodology",
        ["minimum viable product"]            = "mvp methodology",
        ["mvp scope definition"]              = "mvp methodology",
        ["mvp scope"]                         = "mvp methodology",
        ["mvp launch"]                        = "mvp methodology",


        ["bmc"]                               = "business model canvas",
        ["lean canvas"]                       = "business model canvas",
        ["business modeling"]                 = "business model canvas",


        ["monetization"]                      = "mobile monetization",
        ["monetization strategy"]             = "mobile monetization",
        ["financial analysis"]                = "financial modeling",
        ["unit economics modeling"]           = "financial modeling",


        ["fast iteration"]                    = "rapid iteration",
        ["iterative development"]             = "rapid iteration",
        ["lean methodology"]                  = "rapid iteration",


        ["agile/scrum"]                       = "agile",
        ["scrum methodology"]                 = "scrum",
        ["scrum framework"]                   = "scrum",
        ["agile methodology"]                 = "agile",


        ["product discovery"]                 = "customer discovery",
        ["discovery research"]                = "customer discovery",
        ["цільова аудиторія"]                 = "customer discovery",


        ["конкурентний аналіз"]               = "market analysis",
        ["ринкові дослідження"]               = "market analysis",
        ["аналіз конкурентів"]                = "market analysis",
        ["аналіз ринку"]                      = "market analysis",


        ["stakeholder management"]            = "stakeholder management",
        ["управління стейкхолдерами"]         = "stakeholder management",
        ["комунікація зі стейкхолдерами"]     = "stakeholder management",


        ["backlog management"]                = "prioritization",
        ["product backlog"]                   = "prioritization",
        ["feature prioritization"]            = "prioritization",
        ["ice prioritization"]                = "prioritization",
        ["rice prioritization"]               = "prioritization",
        ["moscow"]                            = "prioritization",


        ["funnel optimization"]               = "funnel analysis",
        ["conversion optimization"]           = "funnel analysis",
        ["воронка"]                           = "funnel analysis",
        ["воронка конверсії"]                 = "funnel analysis",


        ["okr planning"]                      = "okr",
        ["okrs framework"]                    = "okr",
        ["kpi tracking"]                      = "kpi",
        ["kpi measurement"]                   = "kpi",


        ["product analytics"]                 = "product analytics",
        ["data analysis"]                     = "product analytics",
        ["data-driven analysis"]              = "product analytics",
        ["product metrics"]                   = "product analytics",


        ["spreadsheet modeling"]              = "excel",
        ["financial modeling in excel"]       = "excel",
    };
}
