using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class SalesVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Sales;
    public string Version => "sales_v1";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first, then desc:\n" +
            "     \"Junior\" / \"SDR\" / \"BDR\" / \"Стажер\"        → \"junior\"\n" +
            "     \"Account Manager\" / \"Sales Rep\" / \"Менеджер з продажу\" → \"middle\"\n" +
            "     \"Senior\" / \"Key Account\" / \"Старший\"        → \"senior\"\n" +
            "     \"Head of Sales\" / \"Sales Director\" / \"CRO\"  → \"lead\"\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "Sales canonicalization:\n" +
            "  \"Salesforce CRM\"                              → \"Salesforce\"\n" +
            "  \"HubSpot CRM\"                                  → \"HubSpot\"\n" +
            "  \"AMOcrm\" / \"amocrm\"                           → \"amoCRM\"\n" +
            "  \"холодні дзвінки\"                              → \"cold calling\"\n" +
            "  \"воронка продажу\"                              → \"sales pipeline\"\n" +
            "  \"роботи з запереченнями\"                       → \"objection handling\"\n" +
            "  \"закриття угод\"                                → \"deal closing\"\n" +
            "  \"генерація лідів\"                              → \"lead generation\"\n" +
            "Keep CRM tool names in their canonical form (Salesforce, HubSpot,\n" +
            "Pipedrive, Zoho, Bitrix24, amoCRM, NetSuite).",

        MustVsNiceMarkers:
            "Standard markers — \"Вимоги:\", \"Required:\", \"Буде плюсом:\",\n" +
            "\"Will be a plus:\". Sales JDs often require minimum quota / revenue\n" +
            "experience (e.g., \"закривав угоди $50k+\"); treat as must-have when\n" +
            "stated as requirement.",

        AntiRequirementsGuide:
            "Sales-specific anti_requirements:\n" +
            "  \"only with own client base\" → portable book of business required\n" +
            "  \"experience exclusively in casino/gambling\" → niche-only background\n" +
            "  \"власне авто обов'язково\" → personal-vehicle requirement\n" +
            "  \"готовність до відряджень 50%\" → high travel requirement",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER — drop universal personality traits, BUT keep\n" +
            "domain-specific competencies. Filter OUT:\n" +
            "  \"комунікабельність\" / \"communication skills\"\n" +
            "  \"відповідальність\" / \"responsibility\"\n" +
            "  \"стресостійкість\" / \"stress tolerance\"\n" +
            "  \"командний гравець\" / \"team player\"\n" +
            "  \"проактивність\" / \"proactivity\"\n" +
            "  \"бажання вчитися\"\n" +
            "  \"вища освіта\" (education_required field).\n" +
            "KEEP these (sales-specific real competencies):\n" +
            "  \"negotiation\" / \"переговори\"\n" +
            "  \"objection handling\" / \"робота з запереченнями\"\n" +
            "  \"closing techniques\" / \"закриття угод\"\n" +
            "  \"client relationship management\"\n" +
            "  \"presentation skills\" (when paired with named context, e.g.\n" +
            "   \"client-facing presentations\").",

        FullWorkedExample:
            "RAW VACANCY (Sales example — B2B SDR in Ukrainian tech company):\n" +
            "  Title: B2B Sales Representative\n" +
            "  Description:\n" +
            "    Шукаємо B2B Sales Representative у SaaS-проект.\n" +
            "    Чим займатимешся:\n" +
            "      - Лідогенерація через LinkedIn Sales Navigator та cold outreach\n" +
            "      - Ведення sales pipeline у HubSpot CRM\n" +
            "      - Cold calling та booking discovery calls\n" +
            "      - Робота з запереченнями та закриття угод $5k-$50k\n" +
            "      - Звітність у Salesforce, weekly forecasting\n" +
            "      - Координація з SDR-командою та маркетингом\n" +
            "    Вимоги:\n" +
            "      - 2+ роки B2B sales або lead generation\n" +
            "      - Досвід з CRM (HubSpot або Salesforce)\n" +
            "      - Англійська C1 (всі клієнти — англомовні)\n" +
            "      - Розуміння MEDDIC / SPIN sales methodology буде плюсом\n" +
            "    Що пропонуємо:\n" +
            "      - Базова ставка + комісія, без стелі\n" +
            "      - Власна клієнтська база не потрібна\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"uk\",\n" +
            "  \"model_version\": \"vac_v4+sales_v1\",\n" +
            "  \"role_title\": { \"en\": \"B2B Sales Representative\", \"uk\": \"B2B Sales Representative\" },\n" +
            "  \"seniority_required\": \"middle\",\n" +
            "  \"must_have_skills\": [\"B2B sales\", \"lead generation\", " +
                "\"LinkedIn Sales Navigator\", \"cold outreach\", \"cold calling\", " +
                "\"sales pipeline\", \"HubSpot\", \"discovery calls\", " +
                "\"objection handling\", \"deal closing\", \"Salesforce\", " +
                "\"sales forecasting\", \"CRM\"],\n" +
            "  \"nice_to_have_skills\": [\"MEDDIC\", \"SPIN\"],\n" +
            "  \"min_years_experience\": 2,\n" +
            "  \"english_required\": \"C1\",\n" +
            "  \"domain_context\": { \"en\": \"B2B SaaS\", \"uk\": \"B2B SaaS\" }\n" +
            "}\n\n" +

            "EXTRACTION LOGIC for Sales-domain prose:\n" +
            "  - Tool literal anywhere → extract (LinkedIn Sales Navigator, HubSpot CRM, Salesforce)\n" +
            "  - Named sales activity → extract (cold outreach, cold calling, lead gen,\n" +
            "    pipeline management, forecasting, discovery calls, deal closing)\n" +
            "  - Named methodology → extract (MEDDIC, SPIN, BANT, Challenger sale)\n" +
            "  - Domain-specific competency → keep (negotiation, objection handling)\n" +
            "  - Generic prose without literal token → extract nothing\n\n" +

            "RESULT: 13 must_have + 2 nice_to_have = 15 named sales skills."
    );
}
