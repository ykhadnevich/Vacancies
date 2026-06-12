using Application.Common.CvNormalization;
using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;

/// <summary>
/// Domain module for Product Management CVs (Product Manager, Product Owner,
/// Product Designer with PM responsibilities, Group PM, Head of Product, CPO).
/// Calibrates the CV normalisation prompt with PM-specific canonicalisation
/// (OKRs, JTBD, prioritisation frameworks, analytics stacks), a soft-trait
/// filter that PRESERVES PM-domain competencies (stakeholder management,
/// product discovery, experimentation) while dropping the generic
/// personality fluff, and a worked example tuned for a Senior PM CV.
/// </summary>
public sealed class ProductCvNormalizationModule : ICvNormalizationModule
{
    public CvDomain Domain => CvDomain.Product;

    public string Version => "product_v1";

    public CvNormalizationSlots GetSlots() => new(
        SeniorityBands:
            "weighted years across all experience. Multipliers: PRODUCTION 1.0, " +
            "FREELANCE 0.7, INTERNSHIP 0.5, PET_PROJECT 0.2, COURSE 0.0. Bands: " +
            "junior = 0–1 yr weighted, middle = 2–4 yrs, senior = 5+ yrs. " +
            "PM-specific note: title alone is NOT enough — a CV that says " +
            "\"Senior Product Manager\" but shows 1 year of PRODUCTION + 2 years " +
            "of internships → middle, not senior. Conversely, an Associate PM " +
            "title with 5+ years of solid PRODUCTION PM work → senior. Derive " +
            "from weighted years and the SCOPE of ownership (team size, ARR, " +
            "user base) — not from the printed title.",

        EducationRelevanceGuide:
            "true if the program's typical curriculum prepares the candidate for " +
            "Product Management. Relevant: MBA, Management, Business " +
            "Administration, Economics, Marketing, Computer Science, Software " +
            "Engineering, Data Science, Information Systems, Mathematics, " +
            "Engineering (any branch when paired with product target), HCI / UX " +
            "design. MBA is HIGHLY relevant for PM, head-of-product, and growth " +
            "targets. A purely humanities degree (philology, history, art " +
            "history) → is_relevant = false unless paired with explicit PM " +
            "retraining (course / bootcamp).",

        TargetRolesGuidance:
            "SOURCE PRIORITY for what the candidate TARGETS (not what they did):\n" +
            "    (1) CV header tagline (the line under the name).\n" +
            "    (2) Professional summary's stated objective.\n" +
            "    (3) Title of the most recent training program / course.\n" +
            "  DO NOT include past job titles from experience entries — those " +
            "describe what the candidate DID, not what they target now.\n" +
            "  Always include level qualifiers (\"Junior\", \"Senior\", " +
            "\"Lead\", \"Principal\", \"Head of\", \"Group\", \"VP\") AND " +
            "function qualifiers (\"Mobile\", \"Growth\", \"B2B\", \"B2C\", " +
            "\"Platform\", \"API\", \"Data\", \"AI\") when they appear in the " +
            "header, summary, or training title — they carry real targeting " +
            "signal that downstream scoring uses.\n" +
            "  PM-specific note: a CV header \"Senior PM · B2B SaaS\" carries " +
            "BOTH the level (senior) AND the segment (B2B SaaS). Preserve both.\n" +
            "  ALSO note any explicit NOT-interested-in list (\"NOT interested " +
            "in: Growth-only PM, BizDev-heavy PM\") — these go into " +
            "target_roles ONLY indirectly: do not list them as targets, but if " +
            "the candidate explicitly excludes a sub-domain, the remaining " +
            "targets become more specific.\n" +
            "  Worked example:\n" +
            "    Header:   \"Senior Product Manager · B2B SaaS · 5.5 years\"\n" +
            "    Summary:  \"...ownership end-to-end від discovery до GA-launch\"\n" +
            "    Target Roles section: \"Senior PM · Principal PM · Head of Product\"\n" +
            "    Past role: \"Associate Product Manager\" (in experience)\n" +
            "    → target_roles = [\"Senior Product Manager\",\n" +
            "                      \"Principal Product Manager\",\n" +
            "                      \"Head of Product\"]\n" +
            "      NOT including \"Associate Product Manager\" — past role.\n" +
            "      Order follows the candidate's own emphasis.",

        ExperienceTypeNotes:
            "PRODUCTION is the dominant type for PM CVs — full-time PM roles at " +
            "real companies with paying users. Apply weight 1.0.\n" +
            "INTERNSHIP for explicit \"Product Management Intern\" / " +
            "\"APM Internship\" entries.\n" +
            "COURSE for \"Mobile PM Course\", \"Reforge\", \"Lenny's Bootcamp\", " +
            "\"Genesis MVP Camp\" and similar structured training programs " +
            "(even when run by a brand-name accelerator).\n" +
            "PET_PROJECT for self-initiated side products without paying users " +
            "(Indie Hackers launches without revenue, GitHub-hosted experiments).\n" +
            "FREELANCE for paid product-consulting engagements where the " +
            "candidate was hired as a PM-for-hire to ship a deliverable.",

        CanonicalizationExamples:
            "Worked examples for PM CVs:\n" +
            "    \"Product Discovery\" + \"customer discovery\" + \"discovery practice\"\n" +
            "        → \"Product discovery\"\n" +
            "    \"OKR\" + \"OKRs\" + \"Objectives and Key Results\" + \"OKR-based planning\"\n" +
            "        → \"OKRs\"\n" +
            "    \"Roadmapping\" + \"roadmap ownership\" + \"theme-based roadmapping\"\n" +
            "        → \"Roadmapping\"\n" +
            "    \"PRD writing\" + \"PRD/RFC writing\" + \"product specs\"\n" +
            "        → \"PRD writing\"\n" +
            "    \"A/B testing\" + \"A/B-testing infrastructure\" + \"A/B testing design\"\n" +
            "        → \"A/B testing\"\n" +
            "    \"Cohort analysis\" + \"cohort retention analysis\"\n" +
            "        → \"Cohort analysis\"\n" +
            "    \"RICE\" + \"RICE prioritization\" + \"RICE framework\"\n" +
            "        → \"RICE\"\n" +
            "    \"MoSCoW\" + \"MoSCoW prioritization\"\n" +
            "        → \"MoSCoW\"\n" +
            "    \"Agile\" + \"Scrum\" + \"Agile/Scrum methodology\"\n" +
            "        → \"Agile/Scrum\"\n" +
            "    \"Hypothesis formulation\" + \"Hypothesis validation\"\n" +
            "        → \"Hypothesis validation\"\n" +
            "  CRITICAL — DO NOT extract feature names, project codenames, or " +
            "deliverables as skills:\n" +
            "    \"Multi-currency support\" — this is a ROADMAP ITEM, not a skill.\n" +
            "    \"Smart Tariff Switching\" — this is the NAME of a feature shipped.\n" +
            "    \"First-time UX nudges\" — this is a feature, not a skill.\n" +
            "    \"GA-launch\" — this is an event type (general availability " +
            "launch), not a competency. Drop unless paired with \"GA release " +
            "process\" or similar named practice.\n" +
            "  These belong implicitly inside experience bullets, NEVER inside " +
            "any of the three skill lists.",

        FullWorkedExample: IrynaSeniorPmWorkedExample);

    private const string IrynaSeniorPmWorkedExample =
        "Sample CV input (Senior PM, B2B SaaS, 5.5 yrs PRODUCTION):\n" +
        "\n" +
        "    Iryna Mykhailenko | Senior Product Manager · B2B SaaS · 5.5 years\n" +
        "    Kyiv, Ukraine · open to remote / hybrid\n" +
        "\n" +
        "    SUMMARY\n" +
        "    Senior PM з 5.5 роками комерційного PM-досвіду у B2B SaaS\n" +
        "    компаніях. Спеціалізуюся на data-driven продуктовому плануванні:\n" +
        "    OKR/north-star metrics, A/B-test культура з 0, ownership end-to-end\n" +
        "    від discovery до GA-launch.\n" +
        "\n" +
        "    EXPERIENCE\n" +
        "    Senior Product Manager — Fintech Pulse (B2B accounting API)\n" +
        "      Jan 2024 — Present · Kyiv (hybrid) · PRODUCTION\n" +
        "      • Власник roadmap-у \"Multi-currency support\" — discovery з\n" +
        "        14 design-partners, spec'd 6-month roadmap, shipped перші\n" +
        "        3 milestone-и за 4 місяці.\n" +
        "      • Запустила A/B-testing infrastructure через GrowthBook +\n" +
        "        Amplitude. 22 експерименти, 9 з статистично значущим winner.\n" +
        "      • Стек: Amplitude, Mixpanel, Tableau, SQL, Notion, Linear,\n" +
        "        Figma (review), Postman, Stripe Billing.\n" +
        "\n" +
        "    Product Manager — Octopus Energy Ukraine\n" +
        "      Apr 2022 — Dec 2023 · Kyiv · PRODUCTION\n" +
        "      • Own-product \"Smart Tariff Switching\" — recommendation engine.\n" +
        "        Hypothesis-testing у 5 cohorts, shipped до 100% UA user base.\n" +
        "      • Стек: Amplitude, Mixpanel, ContentSquare, Figma, Jira,\n" +
        "        Confluence, SQL.\n" +
        "\n" +
        "    Associate Product Manager — EPAM Systems\n" +
        "      Mar 2020 — Mar 2022 · Lviv · PRODUCTION\n" +
        "      • Discovery + spec нового workflow для project staffing —\n" +
        "        раніше manual через Excel.\n" +
        "      • Brand-new metric framework — usage funnel + retention + NPS.\n" +
        "\n" +
        "    Product Management Intern — Genesis Investments\n" +
        "      Jun 2019 — Aug 2019 · Kyiv · INTERNSHIP\n" +
        "      • Власна mini-feature \"First-time UX nudges\" — shipped,\n" +
        "        +3% D7 retention.\n" +
        "\n" +
        "    SKILLS\n" +
        "    Product: Discovery (user interviews, design partners),\n" +
        "      roadmapping (OKRs, theme-based), PRD/RFC writing,\n" +
        "      RICE/MoSCoW prioritization, sprint planning + retrospectives.\n" +
        "    Analytics: A/B testing design, causal inference basics,\n" +
        "      cohort analysis, funnel decomposition, retention curves.\n" +
        "    Tools: Amplitude, Mixpanel, Tableau, Looker (basic), SQL,\n" +
        "      Figma (review), Notion, Linear, Jira, GrowthBook, Stripe Billing.\n" +
        "    Domain: B2B SaaS GTM, API products, fintech basics, energy retail.\n" +
        "    Soft: Cross-functional leadership, C-level communication,\n" +
        "      async-first documentation culture.\n" +
        "\n" +
        "    EDUCATION\n" +
        "    Kyiv School of Economics (KSE)\n" +
        "      Master's in Management (MBA-track) · 2017–2019 · Completed.\n" +
        "    National University of Kyiv-Mohyla Academy (NaUKMA)\n" +
        "      Bachelor's in Economics · 2013–2017 · Completed.\n" +
        "\n" +
        "    LANGUAGES\n" +
        "    Ukrainian — native\n" +
        "    English — C1 (Advanced)\n" +
        "    Polish — A2\n" +
        "\n" +
        "    TARGET ROLES\n" +
        "    Senior PM · Principal PM · Head of Product (smaller team).\n" +
        "\n" +
        "Ideal extraction:\n" +
        "\n" +
        "    {\n" +
        "      \"seniority\": \"senior\",\n" +
        "      \"target_roles\": [\"Senior Product Manager\",\n" +
        "                       \"Principal Product Manager\",\n" +
        "                       \"Head of Product\"],\n" +
        "      \"domain_skills\": [\n" +
        "        \"Product discovery\", \"OKRs\", \"Roadmapping\", \"PRD writing\",\n" +
        "        \"RICE\", \"MoSCoW\", \"Sprint planning\", \"Retrospectives\",\n" +
        "        \"A/B testing\", \"Cohort analysis\", \"Funnel decomposition\",\n" +
        "        \"Retention curves\", \"Hypothesis validation\",\n" +
        "        \"GrowthBook\", \"Amplitude\", \"Mixpanel\", \"Tableau\",\n" +
        "        \"SQL\", \"Notion\", \"Linear\", \"Figma\", \"Jira\",\n" +
        "        \"Confluence\", \"Stripe Billing\", \"Postman\",\n" +
        "        \"ContentSquare\", \"Causal inference\",\n" +
        "        \"B2B SaaS GTM\", \"API products\"\n" +
        "      ],\n" +
        "      \"technical_skills\": [\n" +
        "        \"Looker\"\n" +
        "      ],\n" +
        "      \"unverified_skills\": [\n" +
        "        \"Cross-functional leadership\", \"C-level communication\",\n" +
        "        \"Async-first documentation\"\n" +
        "      ],\n" +
        "      \"experience\": [\n" +
        "        {\"title\": \"Fintech Pulse (B2B accounting API)\", \"type\": \"PRODUCTION\", \"duration_months\": 30, \"years_ago\": 0},\n" +
        "        {\"title\": \"Octopus Energy Ukraine\", \"type\": \"PRODUCTION\", \"duration_months\": 20, \"years_ago\": 2},\n" +
        "        {\"title\": \"EPAM Systems (B2B internal tools)\", \"type\": \"PRODUCTION\", \"duration_months\": 25, \"years_ago\": 4},\n" +
        "        {\"title\": \"Genesis Investments\", \"type\": \"INTERNSHIP\", \"duration_months\": 3, \"years_ago\": 7}\n" +
        "      ],\n" +
        "      \"education\": {\n" +
        "        \"degree\": \"master\",\n" +
        "        \"field\": \"Management (MBA-track)\",\n" +
        "        \"is_relevant\": true,\n" +
        "        \"status\": \"completed\",\n" +
        "        \"current_year\": null,\n" +
        "        \"graduation_year\": 2019\n" +
        "      },\n" +
        "      \"english_level\": \"C1\",\n" +
        "      \"languages\": [\n" +
        "        {\"language\": \"Ukrainian\", \"level\": \"native\"},\n" +
        "        {\"language\": \"English\", \"level\": \"C1\"},\n" +
        "        {\"language\": \"Polish\", \"level\": \"A2\"}\n" +
        "      ],\n" +
        "      \"has_real_product_experience\": true,\n" +
        "      \"career_switcher\": false\n" +
        "    }\n" +
        "\n" +
        "Key decisions demonstrated in this example:\n" +
        "  - seniority = senior: 5.5 yrs PRODUCTION PM × 1.0 = 5.5 weighted years.\n" +
        "    The title \"Senior\" matches but is NOT the deciding factor — the\n" +
        "    weighted years cross the 5+ band threshold on their own.\n" +
        "  - target_roles taken from explicit TARGET ROLES section + header,\n" +
        "    NOT from past job titles. \"Associate Product Manager\" is past role,\n" +
        "    not a target.\n" +
        "  - \"Looker\" is the ONLY technical_skill: it appears only in the Tools\n" +
        "    list as \"(basic)\" and is NOT referenced in any experience bullet.\n" +
        "    Everything else either has bullet evidence (→ domain_skills) or is\n" +
        "    a trait (→ unverified_skills).\n" +
        "  - \"Multi-currency support\", \"Smart Tariff Switching\",\n" +
        "    \"First-time UX nudges\" are FEATURE NAMES extracted from bullets —\n" +
        "    they appear in the experience.title implicitly but MUST NOT pollute\n" +
        "    the skill lists. They are roadmap items / shipped features, not\n" +
        "    competencies.\n" +
        "  - \"GA-launch\" is dropped entirely: it is an event type (general\n" +
        "    availability launch), not a named methodology or tool.\n" +
        "  - duration_months computed from explicit date ranges (Jan 2024 →\n" +
        "    Jun 2026 ≈ 30; Apr 2022 → Dec 2023 ≈ 20; Mar 2020 → Mar 2022 = 24,\n" +
        "    rounded to 25 on month boundaries).\n" +
        "  - years_ago = years between TODAY and the entry's END. Genesis ended\n" +
        "    Aug 2019 → 2026-2019 = 7 years_ago.\n" +
        "  - has_real_product_experience = true: three PRODUCTION entries.\n" +
        "  - career_switcher = false: every entry is in product management.";
}
