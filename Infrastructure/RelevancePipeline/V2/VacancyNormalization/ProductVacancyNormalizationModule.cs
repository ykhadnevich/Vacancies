using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


/// <summary>
/// Domain module for Product Management roles (Product Manager, Product Owner,
/// Product Designer with PM responsibilities, Group PM, Head of Product, CPO).
/// Calibrates the normalisation prompt with PM-specific seniority cues, skill
/// canonicalisation (OKRs, Jobs-to-be-Done, prioritisation frameworks, analytics
/// stacks), and a soft-trait filter that PRESERVES PM-domain competencies like
/// stakeholder management and discovery while dropping generic personality fluff.
/// </summary>
public sealed class ProductVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Product;
    public string Version => "product_v1";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first, then desc:\n" +
            "     \"Associate PM\" / \"Junior PM\" / \"APM\"             → \"junior\"\n" +
            "     \"Product Manager\" / \"Product Owner\" / \"PM\" / \"PO\" → \"middle\"\n" +
            "     \"Senior PM\" / \"Senior Product Manager\" / \"Senior PO\" → \"senior\"\n" +
            "     \"Lead PM\" / \"Principal PM\" / \"Group PM\" /\n" +
            "     \"Head of Product\" / \"VP Product\" / \"CPO\" / \"Director of Product\" → \"lead\"\n" +
            "   Note: a plain \"Product Manager\" / \"Product Owner\" with NO level prefix\n" +
            "   normally implies middle (3+ years commercial product experience expected\n" +
            "   by Ukrainian-market default). Do NOT mark it junior.\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "Product canonicalization (canonical form on RIGHT):\n" +
            "  \"Product Owner\" / \"PO\"                          → \"product ownership\"\n" +
            "  \"Product Discovery\" / \"discovery practice\"      → \"product discovery\"\n" +
            "  \"Jobs To Be Done\" / \"JTBD\"                       → \"JTBD\"\n" +
            "  \"OKR\" / \"OKRs\" / \"Objectives and Key Results\"   → \"OKRs\"\n" +
            "  \"PRD\" / \"Product Requirements Document\"        → \"PRD writing\"\n" +
            "  \"user stories\" / \"acceptance criteria\"         → \"user stories\"\n" +
            "  \"роадмап\" / \"roadmapping\"                       → \"roadmapping\"\n" +
            "  \"user research\" / \"customer interviews\"        → \"user research\"\n" +
            "  \"A/B testing\" / \"split testing\"                 → \"A/B testing\"\n" +
            "  \"experimentation\" / \"hypothesis testing\"       → \"experimentation\"\n" +
            "  \"backlog grooming\" / \"refinement\"               → \"backlog refinement\"\n" +
            "  \"stakeholder management\"                       → \"stakeholder management\"\n" +
            "  \"MVP scoping\" / \"MVP definition\"                → \"MVP scoping\"\n" +
            "  \"go-to-market\" / \"GTM\"                          → \"GTM strategy\"\n" +
            "  \"продакт-менеджер\" / \"продукт-менеджер\"        → \"product management\"\n" +
            "  \"продакт-овнер\"                                 → \"product ownership\"\n" +
            "Tool canonicalisation:\n" +
            "  \"Mixpanel\" / \"Amplitude\" / \"Heap\"               → analytics tool name as-is\n" +
            "  \"Jira\" / \"Linear\" / \"Asana\" / \"ClickUp\"         → PM tool name as-is\n" +
            "  \"Figma\" / \"FigJam\" / \"Miro\" / \"Whimsical\"       → collaboration tool name as-is\n" +
            "  \"Notion\" / \"Confluence\"                         → spec tool name as-is\n" +
            "Prioritisation frameworks (keep verbatim):\n" +
            "  RICE, ICE, MoSCoW, Kano, Weighted Shortest Job First (WSJF), Value vs Effort\n" +
            "Methodologies (keep verbatim):\n" +
            "  Dual-track Agile, Lean Startup, Design Sprint, Continuous Discovery",

        MustVsNiceMarkers:
            "Standard markers — \"Вимоги:\", \"Required:\", \"Буде плюсом:\",\n" +
            "\"Will be a plus:\", \"Nice to have:\". Product JDs often phrase\n" +
            "experience requirements implicitly (\"You've shipped a 0→1 product\",\n" +
            "\"Owned a product from discovery to GA\") — treat as must-have when\n" +
            "stated as expectation, not as nice-to-have.",

        AntiRequirementsGuide:
            "Product-specific anti_requirements:\n" +
            "  \"engineering background required\" / \"CS degree mandatory\" →\n" +
            "    blocker for non-tech PMs (rare; only treat as hard requirement\n" +
            "    when the JD is explicit about it being mandatory).\n" +
            "  \"experience exclusively in iGaming / casino\" → niche-only background\n" +
            "  \"must have shipped a regulated fintech product\" → domain lock-in\n" +
            "  \"onsite Tel Aviv / NYC / SF only\" → relocation requirement\n" +
            "  \"only with prior CPO / VP Product experience\" → seniority lock-in",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER — drop universal personality traits, BUT keep\n" +
            "PM-domain competencies. Filter OUT:\n" +
            "  \"комунікабельність\" / \"communication skills\"\n" +
            "  \"відповідальність\" / \"ownership mindset\" (generic version)\n" +
            "  \"командний гравець\" / \"team player\"\n" +
            "  \"проактивність\" / \"proactivity\"\n" +
            "  \"бажання вчитися\" / \"growth mindset\"\n" +
            "  \"leadership\" (generic, without team-size context)\n" +
            "  \"вища освіта\" (education_required field).\n" +
            "KEEP these (PM-specific real competencies — they map to behaviours,\n" +
            "not personality):\n" +
            "  \"stakeholder management\" / \"робота зі стейкхолдерами\"\n" +
            "  \"cross-functional collaboration\"\n" +
            "  \"product discovery\" / \"customer discovery\"\n" +
            "  \"experimentation mindset\" (paired with \"A/B\" or \"hypotheses\")\n" +
            "  \"data-informed decision making\" (paired with named analytics tool)\n" +
            "  \"prioritisation\" (when paired with a named framework)\n" +
            "  \"product strategy\" / \"GTM strategy\"\n" +
            "  \"strategic thinking\" (only when paired with \"product\" or \"roadmap\").",

        FullWorkedExample:
            "RAW VACANCY (Product example — Senior PM at a B2B SaaS startup):\n" +
            "  Title: Senior Product Manager\n" +
            "  Description:\n" +
            "    Шукаємо Senior Product Manager у наш B2B SaaS-продукт для\n" +
            "    середнього бізнесу. Команда: 8 інженерів, 2 дизайнери,\n" +
            "    1 data analyst.\n" +
            "    Чим займатимешся:\n" +
            "      - Owning product strategy та roadmap (поквартальний цикл, OKRs)\n" +
            "      - Continuous product discovery: customer interviews 5+ на тиждень\n" +
            "      - Writing PRDs та user stories для розробки\n" +
            "      - Backlog grooming, prioritisation за RICE\n" +
            "      - A/B testing і experimentation у Amplitude та Mixpanel\n" +
            "      - Cross-functional collaboration з engineering, design, sales, CS\n" +
            "      - GTM strategy для запуску нових feature-ів\n" +
            "      - Stakeholder management — звітність CPO і фаундерам\n" +
            "    Вимоги:\n" +
            "      - 4+ роки product management у B2B SaaS\n" +
            "      - Shipped хоча б один 0→1 продукт або major redesign\n" +
            "      - Сильні навички у product discovery (customer interviews, JTBD)\n" +
            "      - Досвід з analytics tools (Amplitude / Mixpanel / Heap)\n" +
            "      - Англійська C1 (всі стейкхолдери — англомовні)\n" +
            "      - Знання prioritisation frameworks (RICE / ICE / Kano)\n" +
            "      - Figma на рівні reading specs (не designing)\n" +
            "    Буде плюсом:\n" +
            "      - Досвід у dual-track Agile\n" +
            "      - Background в engineering або data\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"uk\",\n" +
            "  \"model_version\": \"vac_v4+product_v1\",\n" +
            "  \"role_title\": { \"en\": \"Senior Product Manager\", \"uk\": \"Senior Product Manager\" },\n" +
            "  \"seniority_required\": \"senior\",\n" +
            "  \"must_have_skills\": [\"product management\", \"product strategy\", " +
                "\"roadmapping\", \"OKRs\", \"product discovery\", \"customer interviews\", " +
                "\"JTBD\", \"PRD writing\", \"user stories\", \"backlog refinement\", " +
                "\"RICE\", \"A/B testing\", \"experimentation\", \"Amplitude\", " +
                "\"Mixpanel\", \"stakeholder management\", \"cross-functional collaboration\", " +
                "\"GTM strategy\", \"B2B SaaS\", \"Figma\"],\n" +
            "  \"nice_to_have_skills\": [\"dual-track Agile\", \"engineering background\"],\n" +
            "  \"min_years_experience\": 4,\n" +
            "  \"english_required\": \"C1\",\n" +
            "  \"domain_context\": { \"en\": \"B2B SaaS\", \"uk\": \"B2B SaaS\" }\n" +
            "}\n\n" +

            "EXTRACTION LOGIC for Product-domain prose:\n" +
            "  - Tool literal anywhere → extract (Amplitude, Mixpanel, Figma, Jira, Notion)\n" +
            "  - Named PM activity → extract (discovery, roadmapping, A/B testing,\n" +
            "    backlog refinement, PRD writing, GTM)\n" +
            "  - Named methodology / framework → extract (RICE, ICE, MoSCoW, Kano, JTBD,\n" +
            "    Dual-track Agile, Lean Startup, Design Sprint, OKRs)\n" +
            "  - Domain-specific competency → keep (stakeholder management, product discovery,\n" +
            "    experimentation mindset when paired with tooling/methodology)\n" +
            "  - Generic prose (\"strong communication\", \"strategic thinking\" alone) → drop\n" +
            "  - Industry context as skill → extract (\"B2B SaaS\", \"marketplaces\",\n" +
            "    \"consumer mobile\") when it functions as a domain must-have\n\n" +

            "RESULT: 20 must_have + 2 nice_to_have = 22 named product skills."
    );
}
