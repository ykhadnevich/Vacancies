using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public sealed class KeywordCvDomainRouter : ICvDomainRouter
{


    private const int MinAbsoluteKeywordScore = 3;


    private static readonly string[] TechKeywords =
    {

        "c#", "c++", " c ", "python", "javascript", "typescript", "java ", "java,",
        "kotlin", "swift", "go ", "golang", "rust", "ruby", " php", "scala", ".net",
        "dotnet",

        "react", "angular", "vue", "node.js", "nodejs", "asp.net", "spring boot",
        "django", "flask", "rails", "express.js", "next.js", "nuxt",

        " sql", "postgresql", "postgres", "mysql", "mongodb", "redis", "nosql",
        "elasticsearch", "dynamodb",

        "aws", "azure", "gcp ", "google cloud", "docker", "kubernetes", "k8s",
        "terraform", "ansible", "ci/cd", "jenkins", "gitlab ci", "github actions",

        "software engineer", "software developer", "backend developer",
        "frontend developer", "full-stack developer", "full stack developer",
        "fullstack", "devops engineer", "data engineer", "data scientist",
        "machine learning engineer", "ml engineer", "ai engineer",
        "ux designer", "ui designer", "ux/ui designer",
        "qa engineer", "qa automation", "sdet", "site reliability engineer",
        "embedded engineer", "firmware engineer", "security engineer",

        "rest api", "graphql", "microservices", " git ", "github", " agile",
        "scrum", "sprint", "jira", "confluence", "linux", "bash",

        "програміст", "розробник", "інженер-програміст", "тестувальник",
        "девелопер", "айті", "it-компані"
        // NOTE: "product manager", "product owner", "product marketing",
        //       "product designer", "продуктовий менеджер" intentionally
        //       removed from here — they belong to ProductKeywords. A CV
        //       that mentions "product manager" once should NOT inflate
        //       the Tech score; the Product module handles those roles.
    };


    private static readonly string[] ProductKeywords =
    {
        // Title cues
        "product manager", "product owner", "senior product manager",
        "associate product manager", "junior product manager",
        "lead product manager", "principal product manager",
        "head of product", "vp product", "chief product officer", "cpo",
        "group product manager", "director of product",
        "продакт-менеджер", "продукт-менеджер", "продакт-овнер",

        // PM activities & artefacts (must overlap multiple times for hits)
        "product strategy", "product discovery", "продуктова стратегія",
        "roadmap", "роадмап", "okrs", "okr ", "objectives and key results",
        "prd ", "product requirements document", "rfc writing",
        "user stories", "acceptance criteria", "backlog refinement",
        "backlog grooming", "jobs-to-be-done", "jobs to be done", "jtbd",

        // Methodologies & frameworks
        "rice prioritization", "rice framework", "ice score", "moscow",
        "kano model", "dual-track agile", "continuous discovery",
        "lean startup", "design sprint", "north star metric",

        // Analytics / experimentation tooling commonly named in PM CVs
        "mixpanel", "amplitude", "heap analytics", "growthbook",
        "a/b testing", "experimentation", "hypothesis testing",
        "split testing", "cohort analysis", "funnel decomposition",
        "retention curves",

        // Stakeholder context
        "stakeholder management", "cross-functional", "go-to-market",
        "gtm strategy", "0 to 1", "0→1",

        // PM-flavoured tools
        "figjam", "whimsical", "productboard", "pendo", "tableau", "looker"
    };

    public CvDomainDetectionResult Detect(string cvRawText)
    {
        if (string.IsNullOrWhiteSpace(cvRawText))
            return new CvDomainDetectionResult(CvDomain.Generic, 1.0);

        var text = cvRawText.ToLowerInvariant();

        // Score every routed domain. Domain with the most keyword hits wins,
        // provided the hit count clears MinAbsoluteKeywordScore. Otherwise we
        // route to Generic — the catch-all module.
        var scores = new Dictionary<CvDomain, int>
        {
            [CvDomain.Tech]    = CountKeywords(text, TechKeywords),
            [CvDomain.Product] = CountKeywords(text, ProductKeywords)
        };

        // Tie-break order. We list specialised domains BEFORE Tech so that on
        // an exact tie the more specific domain wins. Rationale: a PM CV with
        // technical background reads as "X PM keywords, Y tech keywords" — if
        // X == Y, the recruiter cares about the PM dimension; default to Tech
        // would suppress PM calibration on every borderline CV.
        var tieOrder = new[] { CvDomain.Product, CvDomain.Tech };
        var top = tieOrder
            .Select(d => new KeyValuePair<CvDomain, int>(d, scores[d]))
            .OrderByDescending(kv => kv.Value)
            .First();

        if (top.Value < MinAbsoluteKeywordScore)
            return new CvDomainDetectionResult(CvDomain.Generic, 1.0);

        // Density-normalised confidence — same shape as the original Tech-only
        // implementation but parameterised on the winning domain.
        var per1k = (double)top.Value / Math.Max(1, text.Length / 1000.0);
        var confidence = Math.Min(1.0, per1k / 5.0);
        return new CvDomainDetectionResult(top.Key, confidence);
    }

    private static int CountKeywords(string lowerText, string[] keywords)
    {
        var count = 0;
        foreach (var keyword in keywords)
        {
            if (lowerText.Contains(keyword))
                count++;
        }
        return count;
    }
}
