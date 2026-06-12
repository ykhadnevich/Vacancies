using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class GenericVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Generic;


    public string Version => "generic_v5";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first, then desc:\n" +
            "     \"Junior\" / \"Trainee\" / \"Стажер\" / \"Інтерн\"  → \"junior\"\n" +
            "     \"Middle\" / \"Mid\" / \"Спеціаліст\"               → \"middle\"\n" +
            "     \"Senior\" / \"Sr.\" / \"Старший\" / \"Провідний\"   → \"senior\"\n" +
            "     \"Head of\" / \"Director\" / \"Chief\" / \"Lead\"     → \"lead\"\n" +
            "     \"Intern\" / \"Internship\" / \"Стажування\"        → \"intern\"\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "Generic canonicalization — keep domain vocabulary literal but normalize\n" +
            "obvious typos and abbreviation forms. Skills MAY include soft / business\n" +
            "competencies for non-tech roles (\"negotiation\", \"category management\",\n" +
            "\"client relationship management\"). Latin script preferred but Cyrillic\n" +
            "domain terms allowed if no clean English equivalent exists.",

        MustVsNiceMarkers:
            "Same markers as Tech — \"Вимоги:\", \"Required:\", \"Буде плюсом:\",\n" +
            "\"Nice to have:\", \"Will be a plus:\". Default = must-have when in main\n" +
            "requirements block without qualifier.",

        AntiRequirementsGuide:
            "Generic anti_requirements — onsite-only, language fluency, citizenship,\n" +
            "industry experience hard-excludes, contract-only.",

        FullWorkedExample:
            "RAW VACANCY (Generic / Product Manager example — Ukrainian SME prose):\n" +
            "  Title: Product Manager (Marketing & Growth)\n" +
            "  Description:\n" +
            "    Шукаємо Product Manager у нашу e-commerce команду.\n" +
            "    Що робитимеш:\n" +
            "      - Формувати product roadmap та узгоджувати пріоритети зі stakeholder'ами\n" +
            "      - Проводити інтерв'ю з користувачами для customer discovery\n" +
            "      - Тестувати гіпотези через A/B testing на лендінгах\n" +
            "      - Працювати з Mixpanel та Google Analytics для product analytics\n" +
            "      - Аналізувати воронку конверсії та шукати точки росту\n" +
            "      - Готувати OKR та трекати KPI на квартал\n" +
            "      - Координація задач у Jira, документація у Confluence\n" +
            "    Вимоги:\n" +
            "      - 2+ роки досвіду на ролі product manager або product owner\n" +
            "      - Розуміння unit economics (CAC, LTV, ARPU)\n" +
            "      - Впевнене володіння Excel, SQL для аналітики\n" +
            "      - Англійська рівня Upper-Intermediate\n" +
            "      - Аналітичне мислення та комунікабельність\n" +
            "    Буде плюсом:\n" +
            "      - Досвід з ICE / RICE prioritization\n" +
            "      - Знання Figma для product specs\n" +
            "    Локація: Київ, віддалено.\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"uk\",\n" +
            "  \"model_version\": \"vac_v4+generic_v5\",\n" +
            "  \"role_title\": { \"en\": \"Product Manager\", \"uk\": \"Product Manager\" },\n" +
            "  \"role_title_raw\": \"Product Manager (Marketing & Growth)\",\n" +
            "  \"seniority_required\": \"middle\",\n" +
            "  \"must_have_skills\": [\"product management\", \"product roadmap\", " +
                "\"stakeholder management\", \"customer discovery\", \"A/B testing\", " +
                "\"Mixpanel\", \"Google Analytics\", \"product analytics\", " +
                "\"funnel analysis\", \"OKR\", \"KPI\", \"Jira\", \"Confluence\", " +
                "\"unit economics\", \"CAC\", \"LTV\", \"ARPU\", \"Excel\", \"SQL\"],\n" +
            "  \"nice_to_have_skills\": [\"ICE prioritization\", \"RICE prioritization\", \"Figma\"],\n" +
            "  \"min_years_experience\": 2,\n" +
            "  \"education_required\": \"not_specified\",\n" +
            "  \"english_required\": \"B2\",\n" +
            "  \"location\": { \"city_en\": \"Kyiv\", \"city_uk\": \"Київ\", \"remote\": true, \"hybrid\": false },\n" +
            "  \"domain_context\": { \"en\": \"e-commerce\", \"uk\": \"e-commerce\" },\n" +
            "  \"anti_requirements\": []\n" +
            "}\n\n" +

            "EXTRACTION LOGIC for PM-domain prose (this is the KEY learning):\n" +
            "  When a bullet describes an ACTIVITY using conventional PM lexicon\n" +
            "  AND a recognized named methodology is literally present, extract it:\n" +
            "  - \"Проводити інтерв'ю з користувачами для customer discovery\"\n" +
            "       → extract \"customer discovery\" (the named methodology appears literally)\n" +
            "  - \"Тестувати гіпотези через A/B testing\"\n" +
            "       → extract \"A/B testing\" (named methodology appears literally)\n" +
            "  - \"Працювати з Mixpanel та Google Analytics\"\n" +
            "       → extract BOTH \"Mixpanel\" and \"Google Analytics\" (named tools)\n" +
            "  - \"Аналізувати воронку конверсії\"\n" +
            "       → extract \"funnel analysis\" — conventional PM term for this\n" +
            "          activity; the Ukrainian \"воронку\" maps to canonical English form\n" +
            "  - \"Координація задач у Jira, документація у Confluence\"\n" +
            "       → extract \"Jira\" and \"Confluence\" (literal named tools)\n" +
            "  - \"Формувати product roadmap\" → extract \"product roadmap\"\n" +
            "  - \"OKR та KPI на квартал\" → extract \"OKR\" and \"KPI\"\n" +
            "  - \"unit economics (CAC, LTV, ARPU)\" → extract ALL FOUR: \"unit economics\",\n" +
            "       \"CAC\", \"LTV\", \"ARPU\"\n\n" +

            "WHAT TO STILL FILTER OUT (do NOT extract — same as SoftTraitFilterGuide):\n" +
            "  - \"Аналітичне мислення\" (soft trait — filtered)\n" +
            "  - \"комунікабельність\" (soft trait — filtered)\n" +
            "  - \"стресостійкість\" / \"відповідальність\" / etc. (soft traits)\n" +
            "  - \"вища освіта\" (handled by education_required field)\n\n" +

            "RESULT: 19 must_have + 3 nice_to_have = 22 total named skills. This is\n" +
            "the realistic count for a PM/Marketing JD written in prose. Compare to\n" +
            "the previous v3 extraction style which yielded only 5-7 from the same\n" +
            "prose, leaving the must_have set so thin that any CV would mismatch.\n\n" +

            "RULE OF THUMB for the LITERAL-vs-PARAPHRASE BALANCE in PM domain:\n" +
            "  - If the bullet contains a NAMED METHODOLOGY token (A/B testing,\n" +
            "    customer discovery, ICE, RICE, OKR, KPI, funnel analysis, GTM,\n" +
            "    JTBD, customer journey, retention curve) → extract that token.\n" +
            "  - If the bullet contains a NAMED TOOL (Jira, Confluence, Figma,\n" +
            "    Mixpanel, Amplitude, GA4, Notion, Asana, Trello, Productboard,\n" +
            "    Linear, Tableau, Looker, Excel, SQL, Google Sheets) → extract.\n" +
            "  - If the bullet only describes generic activity prose without any\n" +
            "    named methodology/tool token (\"talk to users\", \"think strategically\",\n" +
            "    \"drive results\") → extract NOTHING (do not invent named concepts).",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER — most job postings (tech AND non-tech) list\n" +
            "universal soft traits in the requirements that are NOT scoreable\n" +
            "skills. These NEVER go into must_have_skills or nice_to_have_skills:\n" +
            "  Personal traits (universal — never a job skill):\n" +
            "    'комунікабельність' / 'communication skills' / 'комунікативні навички'\n" +
            "    'відповідальність' / 'responsibility'\n" +
            "    'командний гравець' / 'team player' / 'робота в команді'\n" +
            "    'уважність до деталей' / 'attention to detail'\n" +
            "    'проактивність' / 'proactivity' / 'high-agency'\n" +
            "    'бажання вчитися' / 'willingness to learn'\n" +
            "    'стресостійкість' / 'stress tolerance'\n" +
            "    'пунктуальність' / 'punctuality'\n" +
            "    'ownership' / 'власність'\n" +
            "    'аналітичне мислення' / 'analytical thinking'\n" +
            "    'чіткі дедлайни' / 'meeting deadlines'\n" +
            "    'multi-tasking' / 'багатозадачність'\n" +
            "    'цілеспрямованість' / 'goal orientation'\n" +
            "    'дисциплінованість' / 'discipline'\n" +
            "    'позитивне ставлення' / 'positive attitude'\n" +
            "  Education tokens (handled by education_required field, not skills):\n" +
            "    'вища освіта' / 'higher education'\n" +
            "    'технічна освіта' / 'technical education'\n" +
            "    'медична освіта' / 'medical education'\n" +
            "    'фармацевтична освіта' / 'pharmaceutical education'\n" +
            "    'бакалавр' / 'магістр' / 'bachelor' / 'master'\n" +
            "  Generic tool literacy (assumed baseline for office roles):\n" +
            "    'впевнений користувач ПК' / 'confident PC user' / 'basic computer skills'\n" +
            "    'MS Office basics' (KEEP specific products: 'Excel' / 'PowerPoint' /\n" +
            "                       'Word' if vacancy lists them as specific requirements)\n\n" +
            "KEEP these — they are real domain-specific skills, not universal traits:\n" +
            "    'negotiation' (sales) / 'переговори'\n" +
            "    'category management' (retail)\n" +
            "    'client relationship management' / 'CRM' (any client-facing role)\n" +
            "    'product strategy', 'product marketing', 'A/B testing' (product roles)\n" +
            "    'market analysis', 'competitor analysis' (when literally named in text)\n" +
            "    Concrete tools: 'Jira', 'Asana', 'Figma', 'Tableau', 'GA4', 'Amplitude'\n\n" +
            "Rule of thumb: if the trait describes a PERSON quality rather than a\n" +
            "MEASURABLE practice or named tool — drop it. The composite scoring\n" +
            "calculator's skill_match formula amplifies pollution: 6 real skills +\n" +
            "10 soft-trait noise items drag a perfect match down from 1.0 to ~0.38."
    );
}
