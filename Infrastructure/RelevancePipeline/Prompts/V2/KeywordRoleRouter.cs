using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class KeywordRoleRouter : IRoleRouter
{
    private const int    TitleWeight        = 5;
    private const int    DescriptionWeight  = 1;
    private const int    MinTopScore        = 3;
    private const double ConfidenceThreshold = 0.30;

    public RoleDetectionResult Detect(string jobTitle, string jobDescription)
    {
        var titleLower = (jobTitle ?? string.Empty).ToLowerInvariant();
        var descLower  = (jobDescription ?? string.Empty).ToLowerInvariant();

        var scores = new Dictionary<RoleFamily, int>
        {
            [RoleFamily.Product]     = Score(titleLower, descLower, ProductKeywords),
            [RoleFamily.Engineering] = Score(titleLower, descLower, EngineeringKeywords),
            [RoleFamily.Data]        = Score(titleLower, descLower, DataKeywords),
            [RoleFamily.Design]      = Score(titleLower, descLower, DesignKeywords),
        };

        var ordered = scores.OrderByDescending(kv => kv.Value).ToList();
        var (topFamily, topScore) = (ordered[0].Key, ordered[0].Value);
        var secondScore = ordered.Count > 1 ? ordered[1].Value : 0;

        if (topScore < MinTopScore)
            return new RoleDetectionResult(RoleFamily.Generic, Confidence: 0.0);

        var confidence = (topScore - secondScore) / (double)topScore;
        if (confidence < ConfidenceThreshold)
            return new RoleDetectionResult(RoleFamily.Generic, Confidence: confidence);

        return new RoleDetectionResult(topFamily, Confidence: confidence);
    }

    private static int Score(string titleLower, string descLower, IReadOnlyList<string> keywords)
    {
        var total = 0;
        foreach (var kw in keywords)
        {
            if (titleLower.Contains(kw)) total += TitleWeight;
            if (descLower.Contains(kw))  total += DescriptionWeight;
        }
        return total;
    }


    private static readonly IReadOnlyList<string> ProductKeywords = new[]
    {

        "product manager", "product owner", "head of product", "chief product",
        "product lead", "associate product", "junior product",
        "product marketing", "growth manager", "growth marketing",
        "business analyst", "system analyst", "systems analyst",
        "project manager", "program manager", "delivery manager",

        "продакт-менеджер", "продакт менеджер", "продакт оунер",
        "менеджер з продукту", "керівник продукту",
        "проджект-менеджер", "проджект менеджер",
        "бізнес-аналітик", "бізнес аналітик", "системний аналітик",
    };

    private static readonly IReadOnlyList<string> EngineeringKeywords = new[]
    {

        "software engineer", "software developer", "developer",
        "backend", "back-end", "back end",
        "frontend", "front-end", "front end",
        "fullstack", "full-stack", "full stack",
        "mobile developer", "ios developer", "android developer",
        "devops", "sre", "site reliability", "platform engineer",
        "qa engineer", "qa automation", "sdet", "test engineer",
        "ml engineer", "machine learning engineer",
        "data engineer",
        "embedded", "firmware engineer",
        "security engineer", "appsec",
        ".net", "java developer", "python developer", "node.js",
        "react developer", "angular developer", "vue developer",

        "розробник", "інженер з розробки", "програміст",
        "бекенд", "фронтенд", "повний стек",
        "тестувальник", "qa спеціаліст",
    };

    private static readonly IReadOnlyList<string> DataKeywords = new[]
    {
        "data analyst", "data scientist", "bi analyst", "business intelligence",
        "analytics engineer", "data analyst",
        "аналітик даних", "дата-аналітик", "дата аналітик",
    };

    private static readonly IReadOnlyList<string> DesignKeywords = new[]
    {
        "ux designer", "ui designer", "ux/ui", "ui/ux",
        "product designer", "graphic designer", "motion designer",
        "brand designer", "visual designer", "interaction designer",
        "user experience", "user interface",
        "дизайнер", "ux-дизайнер", "ui-дизайнер",
    };
}
