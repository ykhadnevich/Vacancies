using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class TechVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Tech;
    public string Version => "tech_v1";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first, then desc text:\n" +
            "     \"Junior\", \"Trainee\", \"Стажер\"              → \"junior\"\n" +
            "     \"Middle\", \"Mid-Level\", \"Strong Junior\"      → \"middle\"\n" +
            "     \"Senior\", \"Sr.\"                              → \"senior\"\n" +
            "     \"Lead\", \"Tech Lead\", \"Тімлід\", \"Principal\", \"Staff\", \"Head of\" → \"lead\"\n" +
            "     \"Intern\", \"Internship\", \"Стажування\"       → \"intern\"\n" +
            "   When title says \"Junior / Middle\" or \"Strong Middle / Senior\":\n" +
            "     take the HIGHER seniority (Senior wins over Middle).\n" +
            "   If \"5+ years\" implies senior but title is generic → \"senior\".\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "Tech canonicalization (canonical form on RIGHT):\n" +
            "  \".Net\" / \"dotnet\" / \".NET Core\" / \".NET 8\"  → \".NET\"\n" +
            "  \"Postgres\" / \"PostgresSQL\"                  → \"PostgreSQL\"\n" +
            "  \"Nodejs\" / \"node.js\"                         → \"Node.js\"\n" +
            "  \"k8s\" / \"kubernetes\"                         → \"Kubernetes\"\n" +
            "  \"Minio\"                                       → \"MinIO\"\n" +
            "  \"github actions\" / \"GitHubActions\"           → \"GitHub Actions\"\n" +
            "  \"EF\" / \"Entity Framework Core\"               → \"EF Core\"\n" +
            "  \"asp.net\" / \"ASP NET Core\"                   → \"ASP.NET Core\"\n" +
            "  \"багатопотоковість\"                            → \"concurrency\"\n" +
            "  \"чиста архітектура\"                            → \"Clean Architecture\"\n" +
            "  \"відмовостійкість\"                             → \"fault tolerance\"\n" +
            "  \"хмарні технології\" / \"хмара\"                 → \"cloud\"\n" +
            "  \"1С\" / \"1S\"                                   → \"1C\"\n" +
            "  \"тімлід\" / \"тех-лід\"                          → \"Tech Lead\"\n" +
            "Skill granularity:\n" +
            "  When the source says \"Kubernetes (Helm, Argo CD, Cilium)\" extract\n" +
            "  EACH as a separate canonical skill — \"Kubernetes\", \"Helm\", \"Argo CD\",\n" +
            "  \"Cilium\". Do not collapse parenthesised stacks into one entry.\n" +
            "When the source lists alternatives like \"Docker or Podman\" — extract\n" +
            "  only the canonical primary (Docker) unless both are listed as required.",

        MustVsNiceMarkers:
            "MUST-HAVE markers (the candidate MUST have this):\n" +
            "  \"Вимоги:\" / \"Required:\" / \"Кваліфікаційні вимоги:\"\n" +
            "  \"Успішний кандидат відповідає:\" / \"Necessary skills:\"\n" +
            "  \"Need:\" / \"Must have:\" / \"Obligatory:\"\n" +
            "  Any skill listed in the main requirements section.\n\n" +
            "NICE-TO-HAVE markers (will-be-a-plus):\n" +
            "  \"Буде плюсом:\" / \"Бажано:\" / \"Перевагою:\"\n" +
            "  \"Will be a plus:\" / \"Nice to have:\" / \"Bonus:\"\n" +
            "  \"Will be considered an advantage:\" / \"Plus:\"\n" +
            "  \"Перевагою буде:\" / \"Додатково:\"\n" +
            "Skills after these markers ALWAYS go to nice_to_have_skills.\n\n" +
            "Default — when a skill is in the bullet list without an explicit marker:\n" +
            "  Treat as must-have UNLESS the surrounding sentence weakens it\n" +
            "  (\"experience would be a plus\", \"familiarity with X is welcome\").",

        AntiRequirementsGuide:
            "Tech-specific anti_requirements examples:\n" +
            "  \"5-7 years contract\" / \"6-month contract\"     → contract-only flag\n" +
            "  \"French-speaking\" / \"German required\"          → language fluency\n" +
            "  \"must be available to come to Norway\"           → travel requirement\n" +
            "  \"onsite only (Kyiv)\" / \"hybrid Warsaw\"          → location lock-in\n" +
            "  \"only veterans / військовослужбовці\"            → military status requirement\n" +
            "  \"волонтерство, немає стабільної ЗП\"              → volunteer / unpaid\n" +
            "  \"PCI DSS / certified\"                             → regulated industry cert\n" +
            "  \"only candidates with EU passport\"               → citizenship lock-in\n" +
            "  \"no consulting/agency background\"                → background hard-exclude\n" +
            "List ONLY actual exclusion criteria — \"Бажаний досвід\" is nice-to-have,\n" +
            "not an anti_requirement.",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER (Tech-specific — Tech vacancies often list soft\n" +
            "traits in the requirements that are NOT real technical requirements):\n" +
            "  Drop entirely from skills:\n" +
            "    \"комунікабельність\" / \"communication skills\"\n" +
            "    \"відповідальність\" / \"responsibility\"\n" +
            "    \"командний гравець\" / \"team player\"\n" +
            "    \"уважність до деталей\" / \"attention to detail\"\n" +
            "    \"проактивність\" / \"proactivity\" / \"high-agency\"\n" +
            "    \"бажання вчитися\" / \"willingness to learn\"\n" +
            "    \"стресостійкість\" / \"stress tolerance\"\n" +
            "  These NEVER go into must_have_skills or nice_to_have_skills.",

        FullWorkedExample:
            "RAW VACANCY (Tech example):\n" +
            "  Title: Senior .NET Engineer\n" +
            "  Description:\n" +
            "    We are looking for a Senior .NET Engineer to join our fintech team.\n" +
            "    Requirements:\n" +
            "      - 5+ years of commercial experience with C# and .NET Core\n" +
            "      - Strong knowledge of ASP.NET Core, EF Core, PostgreSQL\n" +
            "      - Experience with microservices and CI/CD\n" +
            "      - Upper-Intermediate English\n" +
            "    Will be a plus:\n" +
            "      - AWS / Docker / Kubernetes\n" +
            "      - Familiarity with payment systems\n" +
            "    Location: Київ, гібрид (3 дні в офісі / 2 віддалено)\n" +
            "    Бакалавр в IT або суміжній галузі.\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"mixed\",\n" +
            "  \"model_version\": \"vac_v4+tech_v1\",\n" +
            "  \"role_title\": { \"en\": \"Senior .NET Developer\", \"uk\": \"Senior .NET розробник\" },\n" +
            "  \"role_title_raw\": \"Senior .NET Engineer\",\n" +
            "  \"seniority_required\": \"senior\",\n" +
            "  \"must_have_skills\": [\"C#\", \".NET\", \"ASP.NET Core\", \"EF Core\", \"PostgreSQL\", \"microservices\", \"CI/CD\"],\n" +
            "  \"nice_to_have_skills\": [\"AWS\", \"Docker\", \"Kubernetes\", \"payment systems\"],\n" +
            "  \"min_years_experience\": 5,\n" +
            "  \"education_required\": \"bachelor\",\n" +
            "  \"english_required\": \"B2\",\n" +
            "  \"location\": { \"city_en\": \"Kyiv\", \"city_uk\": \"Київ\", \"remote\": false, \"hybrid\": true },\n" +
            "  \"domain_context\": { \"en\": \"fintech\", \"uk\": \"фінтех\" },\n" +
            "  \"anti_requirements\": []\n" +
            "}\n\n" +

            "Anchors to learn from this example:\n" +
            "  - Title \"Senior .NET Engineer\" → role_title.en = \"Senior .NET Developer\"\n" +
            "    (rule 6.1: drop Engineer, swap to Developer).\n" +
            "  - role_title_raw preserved exactly.\n" +
            "  - skills are individual canonical forms, parenthesised stacks split.\n" +
            "  - \"Бакалавр в IT\" → education_required = \"bachelor\".\n" +
            "  - \"Upper-Intermediate\" → english_required = \"B2\".\n" +
            "  - source_language = \"mixed\" because title+requirements are EN but\n" +
            "    location/education line is UK.\n" +
            "  - no anti_requirements because hybrid is normal, not exclusionary."
    );
}
