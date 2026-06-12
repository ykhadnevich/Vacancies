using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class HrVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Hr;
    public string Version => "hr_v1";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first, then desc:\n" +
            "     \"Junior Recruiter\" / \"Sourcer\" / \"Trainee HR\"        → \"junior\"\n" +
            "     \"Recruiter\" / \"HR Specialist\" / \"HR Generalist\"      → \"middle\"\n" +
            "     \"Senior Recruiter\" / \"Lead Recruiter\" / \"HRBP\"       → \"senior\"\n" +
            "     \"Head of People\" / \"Head of HR\" / \"CHRO\"             → \"lead\"\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "HR canonicalization:\n" +
            "  \"ATS\" / \"applicant tracking system\"           → \"ATS\"\n" +
            "  \"Greenhouse ATS\"                               → \"Greenhouse\"\n" +
            "  \"Workable\" / \"Lever\" / \"Recruitee\"            → keep as-is\n" +
            "  \"Boolean search\" / \"X-ray search\"             → \"Boolean search\"\n" +
            "  \"LinkedIn Recruiter\" / \"LI Recruiter\"         → \"LinkedIn Recruiter\"\n" +
            "  \"скринінг\" / \"screening calls\"                → \"screening\"\n" +
            "  \"наймання\" / \"hiring\"                          → \"hiring\"\n" +
            "  \"адаптація\" / \"онбординг\"                      → \"onboarding\"\n" +
            "  \"employer branding\" / \"бренд роботодавця\"     → \"employer branding\"\n" +
            "  \"C&B\" / \"compensation and benefits\"            → \"compensation & benefits\"\n" +
            "  \"performance review\" / \"оцінка персоналу\"     → \"performance management\"\n" +
            "Keep tool/platform names in canonical Latin form.",

        MustVsNiceMarkers:
            "Standard markers — \"Вимоги:\", \"Required:\", \"Буде плюсом:\".\n" +
            "HR JDs often require \"experience hiring for specific role\"; treat as\n" +
            "must-have when stated as requirement, otherwise nice-to-have.",

        AntiRequirementsGuide:
            "HR-specific anti_requirements:\n" +
            "  \"тільки досвід найму IT\" → niche-only role-type requirement\n" +
            "  \"власна база кандидатів обов'язкова\" → portable network required\n" +
            "  \"тільки агенційний досвід\" → agency-only background\n" +
            "  \"має жити в Києві (offline співбесіди)\" → location lock-in",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER — drop universal personality traits. HR is a\n" +
            "tricky case because some \"soft\" traits ARE the job, but most are\n" +
            "still universal and not scoreable:\n" +
            "  Drop:\n" +
            "    \"комунікабельність\" / \"communication skills\"\n" +
            "    \"відповідальність\" / \"responsibility\"\n" +
            "    \"уважність до деталей\" / \"attention to detail\"\n" +
            "    \"стресостійкість\" / \"stress tolerance\"\n" +
            "    \"проактивність\" / \"proactivity\"\n" +
            "    \"вища освіта\" (education_required field).\n" +
            "  KEEP (HR-specific real competencies):\n" +
            "    \"interviewing skills\" / \"navички ведення співбесід\"\n" +
            "    \"employer branding\"\n" +
            "    \"candidate experience\"\n" +
            "    \"stakeholder management\" (talent partnering with hiring managers)\n" +
            "    \"data-driven hiring\" / \"recruiting analytics\"\n" +
            "    \"conflict resolution\" / \"вирішення конфліктів\" (real HR competency).",

        FullWorkedExample:
            "RAW VACANCY (HR example — Tech recruiter in Ukrainian IT company):\n" +
            "  Title: Tech Recruiter\n" +
            "  Description:\n" +
            "    Ми шукаємо Tech Recruiter у швидко зростаючу IT-команду.\n" +
            "    Що робитимеш:\n" +
            "      - Сорсинг кандидатів через LinkedIn Recruiter та Boolean search\n" +
            "      - Скринінг резюме та screening calls (15-20 на тиждень)\n" +
            "      - Ведення кандидатів у ATS (використовуємо Greenhouse)\n" +
            "      - Координація інтерв'ю з hiring managers\n" +
            "      - Робота з recruiting metrics (time-to-hire, conversion rates)\n" +
            "      - Покращення candidate experience та employer branding\n" +
            "      - Адаптація (онбординг) нових співробітників перші 30 днів\n" +
            "    Вимоги:\n" +
            "      - 2+ роки досвіду tech recruiting в IT\n" +
            "      - Досвід з LinkedIn Recruiter та Boolean search\n" +
            "      - Англійська B2 (всі описи позицій — англомовні)\n" +
            "      - Знання recruiting funnels та conversion analytics\n" +
            "    Буде плюсом:\n" +
            "      - Досвід з ATS-системами (Greenhouse, Lever, Workable)\n" +
            "      - Employer branding initiatives\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"uk\",\n" +
            "  \"model_version\": \"vac_v4+hr_v1\",\n" +
            "  \"role_title\": { \"en\": \"Tech Recruiter\", \"uk\": \"Tech Recruiter\" },\n" +
            "  \"seniority_required\": \"middle\",\n" +
            "  \"must_have_skills\": [\"tech recruiting\", \"sourcing\", " +
                "\"LinkedIn Recruiter\", \"Boolean search\", \"screening\", " +
                "\"ATS\", \"Greenhouse\", \"interviewing\", " +
                "\"recruiting metrics\", \"time-to-hire\", " +
                "\"candidate experience\", \"employer branding\", \"onboarding\", " +
                "\"recruiting funnels\", \"conversion analytics\"],\n" +
            "  \"nice_to_have_skills\": [\"Lever\", \"Workable\"],\n" +
            "  \"min_years_experience\": 2,\n" +
            "  \"english_required\": \"B2\",\n" +
            "  \"domain_context\": { \"en\": \"B2B SaaS\", \"uk\": \"B2B SaaS\" }\n" +
            "}\n\n" +

            "EXTRACTION LOGIC for HR-domain prose:\n" +
            "  - Named ATS / platform → extract (Greenhouse, Lever, Workable, LinkedIn Recruiter)\n" +
            "  - HR methodology → extract (sourcing, Boolean search, screening, onboarding)\n" +
            "  - HR metric → extract (time-to-hire, conversion rate, retention rate)\n" +
            "  - Real HR competency → keep (employer branding, candidate experience)\n" +
            "  - Universal soft trait → filter out (communication, responsibility)\n\n" +

            "RESULT: 15 must_have + 2 nice_to_have = 17 named HR skills."
    );
}
