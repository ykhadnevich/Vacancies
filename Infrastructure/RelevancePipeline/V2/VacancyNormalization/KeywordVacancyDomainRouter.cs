using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class KeywordVacancyDomainRouter : IVacancyDomainRouter
{
    private const double MinConfidence = 0.0015;

    private static readonly string[] TechKeywords =
    {

        ".net", "c#", "java", "python", "javascript", "typescript", "kotlin", "swift",
        "golang", " go ", "ruby", "php", "scala", "rust",

        "react", "angular", "vue", "next.js", "nuxt", "asp.net", "spring",
        ".net core", "node.js", "django", "flask", "laravel",

        "kubernetes", "k8s", "docker", "terraform", "ansible", "ci/cd", "aws",
        "azure", "gcp", "jenkins", "gitlab", "github", "helm", "argo",

        "postgresql", "mysql", "mongodb", "redis", "snowflake", "bigquery",

        "machine learning", "ml engineer", "data engineer", "data scientist",
        "pytorch", "tensorflow", "scikit", "pandas", "spark",

        "розробник", "розробка", "програміст", "engineer", "developer",
        "backend", "frontend", "fullstack", "devops", "qa engineer", "sre",
        "tech lead", "тех-лід", "тімлід",

        "agile", "scrum", "rest api", "graphql", "microservices",
    };

    private static readonly string[] HealthcareKeywords =
    {

        "лікар", "медсестра", "пацієнт", "клінічний", "терапевт", "хірург",
        "кардіолог", "стоматолог", "офтальмолог", "невролог", "педіатр",
        "патронаж", "медсестринство",
        "rn ", "nurse", "physician", "doctor", "surgeon", "dentist",
        "pharmacist", "фармацевт", "фармацевтичний",

        "clinical", "patient", "hospital", "medical center", "лікарня",
        "клініка", "медичний центр", "аптек", "pharmacy",

        "медичн", "медоблад", "медичне обладнання", "клінічн дослід",
        "клінічних випробувань", "diagnostics", "медобслуговування"
    };

    private static readonly string[] LegalKeywords =
    {
        "юрист", "адвокат", "правник", "юридич", "compliance officer",
        "legal counsel", "corporate law", "litigation", "paralegal",
        "регуляторн", "regulatory"
    };

    private static readonly string[] EducationKeywords =
    {
        "вчитель", "викладач", "професор", "академіч", "teacher", "professor",
        "lecturer", "education", "school administrator"
    };

    private static readonly string[] SalesKeywords =
    {

        "sales manager", "business development", "account manager",
        "key account", "bizdev", "biz dev", "client manager", "account executive",
        "sales representative", "sales rep", "продавець-консультант",
        "менеджер з продажу", "менеджер по продажах",
        "клієнт-менеджер", "менеджер з роботи з клієнтами",

        "продаж", "b2b sales", "b2c sales", "lead generation", "cold outreach",
        "cold calling", "холодні дзвінки", "пошук клієнтів",
        "sales pipeline", "sales cycle", "квоти продаж", "виконання плану",

        "salesforce", "hubspot crm", "bitrix", "pipedrive", "amocrm"
    };

    private static readonly string[] MarketingKeywords =
    {

        "marketing manager", "digital marketing", "performance marketing",
        "growth marketing", "content marketing", "brand manager",
        "smm", "social media", "smm-фахівець", "контент-менеджер",
        "маркетолог", "бренд-менеджер", "інтернет-маркетолог",
        "performance manager", "ppc specialist", "seo specialist",
        "seo manager", "сео-фахівець", "ppc-фахівець",
        "email marketer", "user acquisition", "affiliate manager",
        "promo manager", "demand generation",

        "рекламн кампан", "ad campaign", "media buying", "медіабаїнг",
        "creative production", "креатив", "брендінг", "rebranding",
        "копірайт", "copywriting", "сторителлінг", "storytelling",

        "google ads", "facebook ads", "meta ads", "tiktok ads",
        "yandex direct", "google tag manager", "gtm tag",
        "search engine optimization", "google search console"
    };

    private static readonly string[] HrKeywords =
    {

        "recruiter", "talent acquisition", "ta specialist", "tech recruiter",
        "hr manager", "hrbp", "hr business partner",
        "people operations", "people ops", "hr generalist", "hr-фахівець",
        "рекрутер", "рекрутинг", "talent sourcer", "сорсер",
        "head of people", "head of talent", "head of hr",
        "compensation and benefits", "c&b specialist",
        "training and development", "l&d specialist",
        "employer branding", "internal communications",

        "screening", "screening calls", "boolean search",
        "candidate experience", "onboarding", "адаптація",
        "performance management", "оцінка персоналу",
        "1:1", "engagement survey",

        "ats ", "applicant tracking", "greenhouse ats", "lever ats",
        "workable", "huntflow", "linkedin recruiter", "linkedin talent",
        "linkedin sales navigator"
    };

    private static readonly string[] FinanceKeywords =
    {
        "бухгалтер", "аудитор", "accountant", "auditor", "financial analyst",
        "investment banker", "credit risk", "trader", "treasury"
    };

    private static readonly string[] DefenceGovKeywords =
    {
        "міністерство оборони", "збройні сили", "deftech", "defence tech",
        "національна гвардія", "укрпошта", "приватбанк", "ощадбанк",
        "дія", "diia", "national bank of ukraine", "військов", "оборонн",
        "державне підприємство", "державний сектор", "ngu ", "ministry of defence",
        "сили безпілотних систем", "miltech"
    };

    private static readonly string[] ProductKeywords =
    {
        // Title cues
        "product manager", "product owner", "associate product manager",
        "senior product manager", "lead product manager", "principal product manager",
        "group product manager", "head of product", "vp product", "vp of product",
        "chief product officer", "cpo", "director of product",
        "продакт-менеджер", "продукт-менеджер", "продакт-овнер",
        "менеджер продукту", "керівник продукту", "керівник напряму",

        // Activities & artefacts
        "product strategy", "product discovery", "продуктова стратегія",
        "roadmap", "роадмап", "okrs", "okr ", "objectives and key results",
        "prd ", "product requirements document",
        "user stories", "acceptance criteria", "backlog refinement",
        "backlog grooming", "jobs-to-be-done", "jobs to be done", "jtbd",

        // Methodologies & frameworks
        "rice prioritization", "rice framework", "ice score",
        "moscow prioritization", "kano model",
        "dual-track agile", "continuous discovery",
        "lean startup", "design sprint",
        "north star metric", "activation rate", "retention curve",

        // Analytics / experimentation
        "mixpanel", "amplitude", "heap analytics",
        "a/b testing", "experimentation", "hypothesis testing",
        "split testing",

        // Stakeholder context
        "stakeholder management", "cross-functional", "go-to-market",
        "gtm strategy", "0 to 1", "0→1", "scaling product",

        // PM-flavoured tools (when mentioned in PM context)
        "figjam", "whimsical", "productboard", "pendo"
    };

    // Title-level phrases that unambiguously identify the ROLE of the vacancy,
    // independent of the surrounding domain context. When any of these appears
    // in the first ~250 chars (where the JD title and headline live), the
    // matching domain wins immediately — no density race.
    private static readonly string[] ProductTitlePhrases =
    {
        "product manager", "product owner", "head of product", "vp product",
        "vp of product", "chief product officer", "director of product",
        "group product manager", "principal product manager", "lead product manager",
        "senior product manager", "associate product manager", "junior product manager",
        "продакт-менеджер", "продукт-менеджер", "продакт-овнер"
    };

    private static readonly string[] SalesTitlePhrases =
    {
        "sales manager", "account executive", "account manager", "key account",
        "business development manager", "bizdev", "sales representative",
        "менеджер з продажу", "менеджер по продажах"
    };

    private static readonly string[] MarketingTitlePhrases =
    {
        "marketing manager", "growth marketing manager", "content marketing manager",
        "brand manager", "performance marketing manager", "smm-фахівець",
        "маркетолог", "бренд-менеджер", "інтернет-маркетолог"
    };

    private static readonly string[] HrTitlePhrases =
    {
        "recruiter", "talent acquisition", "hr manager", "hr business partner",
        "hrbp", "head of people", "head of talent", "head of hr",
        "рекрутер"
    };

    private static bool HasAnyHit(string text, string[] phrases)
    {
        foreach (var p in phrases)
            if (text.Contains(p)) return true;
        return false;
    }

    public VacancyDomainDetectionResult Detect(string vacancyRawText)
    {
        if (string.IsNullOrWhiteSpace(vacancyRawText))
            return new VacancyDomainDetectionResult(VacancyDomain.Generic, 1.0);

        var lower = vacancyRawText.ToLowerInvariant();
        var len = Math.Max(1, lower.Length);

        var scores = new Dictionary<VacancyDomain, int>
        {
            [VacancyDomain.Tech]              = CountHits(lower, TechKeywords),
            [VacancyDomain.Healthcare]        = CountHits(lower, HealthcareKeywords),
            [VacancyDomain.Legal]             = CountHits(lower, LegalKeywords),
            [VacancyDomain.Education]         = CountHits(lower, EducationKeywords),
            [VacancyDomain.Sales]             = CountHits(lower, SalesKeywords),
            [VacancyDomain.Marketing]         = CountHits(lower, MarketingKeywords),
            [VacancyDomain.Hr]                = CountHits(lower, HrKeywords),
            [VacancyDomain.Finance]           = CountHits(lower, FinanceKeywords),
            [VacancyDomain.DefenceGovernment] = CountHits(lower, DefenceGovKeywords),
            [VacancyDomain.Product]           = CountHits(lower, ProductKeywords)
        };

        // Hard-priority for Product / Sales / Marketing / Hr / Healthcare when their
        // TITLE-LEVEL keyword fires AND the keyword density is non-trivial. Without
        // this guard a PM vacancy at a hardware company ("Senior Product Manager —
        // Ajax Intercoms, BLE, Zigbee, embedded firmware") gets routed to Tech
        // because the description body is full of technology nouns, even though
        // the ROLE is unambiguously PM. The router must respect the role, not the
        // surrounding domain context.
        //
        // Check role-tagging phrases in the title / first lines and let the
        // matching domain pre-empt the generic-density race.
        var firstChunk = lower.Length > 250 ? lower[..250] : lower;
        if (HasAnyHit(firstChunk, ProductTitlePhrases))
            return new VacancyDomainDetectionResult(VacancyDomain.Product, 1.0);
        if (HasAnyHit(firstChunk, SalesTitlePhrases))
            return new VacancyDomainDetectionResult(VacancyDomain.Sales, 1.0);
        if (HasAnyHit(firstChunk, MarketingTitlePhrases))
            return new VacancyDomainDetectionResult(VacancyDomain.Marketing, 1.0);
        if (HasAnyHit(firstChunk, HrTitlePhrases))
            return new VacancyDomainDetectionResult(VacancyDomain.Hr, 1.0);

        var top = scores.OrderByDescending(kv => kv.Value).First();
        var density = (double)top.Value / len;

        if (density < MinConfidence)
            return new VacancyDomainDetectionResult(VacancyDomain.Generic, 1.0);

        return new VacancyDomainDetectionResult(top.Key, Math.Min(1.0, density / 0.01));
    }

    private static int CountHits(string text, string[] keywords)
    {
        int total = 0;
        foreach (var kw in keywords)
        {
            int idx = 0;
            while ((idx = text.IndexOf(kw, idx, StringComparison.Ordinal)) >= 0)
            {
                total++;
                idx += kw.Length;
            }
        }
        return total;
    }
}
