using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class HealthcareVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Healthcare;
    public string Version => "healthcare_v1";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first:\n" +
            "     \"Junior\" / \"Інтерн\" / \"Resident\" / \"Ординатор\"      → \"junior\"\n" +
            "     \"Лікар\" (без префіксу) / \"Practitioner\"               → \"middle\"\n" +
            "     \"Senior\" / \"Старший лікар\" / \"Attending\"             → \"senior\"\n" +
            "     \"Head of Department\" / \"Завідувач\" / \"Director\"     → \"lead\"\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "Healthcare canonicalization:\n" +
            "  Medical specialties (canonical English form):\n" +
            "    \"терапія\" → \"internal medicine\"\n" +
            "    \"хірургія\" → \"surgery\"\n" +
            "    \"педіатрія\" → \"pediatrics\"\n" +
            "    \"кардіологія\" → \"cardiology\"\n" +
            "    \"невропатологія\" → \"neurology\"\n" +
            "    \"ортопедія\" → \"orthopedics\"\n" +
            "    \"гінекологія\" → \"gynecology\"\n" +
            "  Equipment / procedures (keep specific terms):\n" +
            "    \"УЗД\" / \"ultrasound\"                          → \"ultrasound diagnostics\"\n" +
            "    \"ЕКГ\" / \"ECG\" / \"EKG\"                         → \"ECG\"\n" +
            "    \"CT-scan\" / \"КТ\"                              → \"CT scan\"\n" +
            "    \"МРТ\" / \"MRI\"                                  → \"MRI\"\n" +
            "    \"клінічні дослідження\" / \"clinical trials\"   → \"clinical trials\"\n" +
            "  Regulatory / standards:\n" +
            "    \"GCP\" / \"Good Clinical Practice\"               → \"GCP\"\n" +
            "    \"ICH\" / \"International Council for Harmonisation\" → \"ICH\"\n" +
            "    \"GMP\" / \"Good Manufacturing Practice\"          → \"GMP\"",

        MustVsNiceMarkers:
            "Standard markers — \"Вимоги:\", \"Кваліфікація:\", \"Required:\".\n" +
            "Healthcare JDs often require licensure (\"чинна ліцензія лікаря\",\n" +
            "\"medical license\", \"сертифікат спеціаліста\"); treat as must-have.",

        AntiRequirementsGuide:
            "Healthcare-specific anti_requirements:\n" +
            "  \"тільки з досвідом у державних установах\" → state-only background\n" +
            "  \"власне обладнання обов'язкове\" → personal-equipment requirement\n" +
            "  \"готовність до нічних чергувань\" → night-shift requirement\n" +
            "  \"тільки з другою категорією\" → category-level requirement",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER — drop universal personality traits. Healthcare is\n" +
            "where soft traits matter clinically, but in JD extraction they're\n" +
            "still not scoreable named skills:\n" +
            "  Drop:\n" +
            "    \"комунікабельність\"\n" +
            "    \"відповідальність\"\n" +
            "    \"уважність до деталей\"\n" +
            "    \"стресостійкість\"\n" +
            "    \"робота в команді\"\n" +
            "  Keep (healthcare-specific real competencies):\n" +
            "    \"patient communication\" / \"спілкування з пацієнтами\"\n" +
            "    \"emergency response\" / \"невідкладна допомога\"\n" +
            "    \"clinical decision-making\"\n" +
            "    \"medical records management\" / \"ведення медичної документації\".\n" +
            "  Education tokens (\"медична освіта\", \"вища медична\") → handled\n" +
            "  by education_required field, NOT in skills array.",

        FullWorkedExample:
            "RAW VACANCY (Healthcare example — Junior medical equipment product manager):\n" +
            "  Title: Продакт-менеджер медичного обладнання (Junior)\n" +
            "  Description:\n" +
            "    Компанія КСЕНКО — постачальник медичного обладнання. Шукаємо Junior PM:\n" +
            "    Чим займатимешся:\n" +
            "      - Підтримка продуктового портфелю (УЗД, ЕКГ, моніторні системи)\n" +
            "      - Розробка маркетингових матеріалів та технічної документації\n" +
            "      - Робота з клінічними дослідженнями та GCP-документацією\n" +
            "      - Аналіз ринку медичного обладнання та конкурентів\n" +
            "      - Супровід тендерних процедур та держзакупівель\n" +
            "      - Координація з лікарями (терапія, кардіологія) для feedback'у\n" +
            "    Вимоги:\n" +
            "      - Вища освіта (медична, технічна або маркетингова)\n" +
            "      - Знання Excel, PowerPoint на впевненому рівні\n" +
            "      - Англійська B1+ (читання технічної документації)\n" +
            "      - Базове розуміння клінічних випробувань\n" +
            "      - Готовність до відряджень по Україні\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"uk\",\n" +
            "  \"model_version\": \"vac_v4+healthcare_v1\",\n" +
            "  \"role_title\": { \"en\": \"Medical Equipment Product Manager (Junior)\", " +
                "\"uk\": \"Продакт-менеджер медичного обладнання (Junior)\" },\n" +
            "  \"seniority_required\": \"junior\",\n" +
            "  \"must_have_skills\": [\"medical equipment\", \"product portfolio management\", " +
                "\"ultrasound diagnostics\", \"ECG\", \"patient monitoring systems\", " +
                "\"marketing collateral\", \"technical documentation\", " +
                "\"clinical trials\", \"GCP\", \"market analysis\", " +
                "\"tender procedures\", \"public procurement\", " +
                "\"Excel\", \"PowerPoint\"],\n" +
            "  \"nice_to_have_skills\": [],\n" +
            "  \"min_years_experience\": 0,\n" +
            "  \"education_required\": \"bachelor\",\n" +
            "  \"english_required\": \"B1\",\n" +
            "  \"anti_requirements\": [\"travel within Ukraine required\"],\n" +
            "  \"domain_context\": { \"en\": \"healthcare\", \"uk\": \"healthcare\" }\n" +
            "}\n\n" +

            "EXTRACTION LOGIC for Healthcare-domain prose:\n" +
            "  - Named medical equipment → extract (УЗД→ultrasound, ECG, MRI)\n" +
            "  - Specialty in parenthesis → extract canonical English\n" +
            "    (\"терапія, кардіологія\" → \"internal medicine\", \"cardiology\")\n" +
            "  - Regulatory standard literal → extract (GCP, ICH, GMP)\n" +
            "  - Business process literal → extract (clinical trials, tender procedures)\n" +
            "  - Education tokens (\"вища медична\") → education_required, NOT skill\n" +
            "  - Generic medical prose → extract nothing (just \"медичний досвід\" alone)\n\n" +

            "RESULT: 14 must_have + 0 nice_to_have = 14 named healthcare skills."
    );
}
