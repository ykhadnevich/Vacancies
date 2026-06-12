using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public sealed class MarketingVacancyNormalizationModule : IVacancyNormalizationModule
{
    public VacancyDomain Domain => VacancyDomain.Marketing;
    public string Version => "marketing_v1";

    public VacancyNormalizationSlots GetSlots() => new(
        SeniorityKeywords:
            "5. seniority_required — detect from title keywords first, then desc:\n" +
            "     \"Junior\" / \"Trainee\" / \"Стажер\"           → \"junior\"\n" +
            "     \"Middle\" / \"Specialist\" / \"Фахівець\"        → \"middle\"\n" +
            "     \"Senior\" / \"Старший\" / \"Lead\"               → \"senior\"\n" +
            "     \"Head of Marketing\" / \"CMO\" / \"Director\"     → \"lead\"\n" +
            "   If nothing detectable → \"not_specified\".",

        SkillCanonicalization:
            "Marketing canonicalization (canonical form on RIGHT):\n" +
            "  \"Google Analytics 4\" / \"GA 4\"                 → \"GA4\"\n" +
            "  \"Facebook Ads\" / \"Meta Ads Manager\"           → \"Meta Ads\"\n" +
            "  \"Google Ads\" / \"AdWords\"                       → \"Google Ads\"\n" +
            "  \"SMM\" / \"Social Media Marketing\"              → \"social media marketing\"\n" +
            "  \"PPC\" / \"Pay-Per-Click\"                        → \"PPC\"\n" +
            "  \"SEO\" / \"Search Engine Optimization\"          → \"SEO\"\n" +
            "  \"медіабаїнг\" / \"медіа-баїнг\"                   → \"media buying\"\n" +
            "  \"копірайтинг\"                                  → \"copywriting\"\n" +
            "  \"інтернет-маркетинг\"                           → \"digital marketing\"\n" +
            "  \"перформанс-маркетинг\"                         → \"performance marketing\"\n" +
            "Skill granularity:\n" +
            "  \"Google Ads (Search, Display, Shopping)\" → extract each:\n" +
            "    \"Google Ads\", \"Google Search Ads\", \"Google Display\", \"Google Shopping\".\n" +
            "  Always keep tool names in Latin script (Meta Ads, GA4, Hotjar).",

        MustVsNiceMarkers:
            "Same markers as Tech — \"Вимоги:\", \"Обов'язково:\", \"Required:\",\n" +
            "\"Буде плюсом:\", \"Nice to have:\", \"Will be a plus:\". Default = must-have\n" +
            "when in the requirements section without explicit weakening phrase.",

        AntiRequirementsGuide:
            "Marketing-specific anti_requirements examples:\n" +
            "  \"only Russian-speaking markets\" → language / market lock-in\n" +
            "  \"onsite only (Kyiv)\" → location lock-in\n" +
            "  \"must have own creative portfolio in iGaming\" → niche-only background\n" +
            "  \"experience exclusively in B2C\" → segment lock-in",

        SoftTraitFilterGuide:
            "SOFT-TRAIT FILTER — drop universal personality traits that aren't skills:\n" +
            "  \"комунікабельність\" / \"communication skills\"\n" +
            "  \"відповідальність\" / \"responsibility\"\n" +
            "  \"командний гравець\" / \"team player\"\n" +
            "  \"уважність до деталей\" / \"attention to detail\"\n" +
            "  \"проактивність\" / \"proactivity\"\n" +
            "  \"креативність\" / \"creativity\" (too generic — keep ONLY if vacancy\n" +
            "                                    explicitly names a creative output\n" +
            "                                    like \"copywriting\" or \"design\")\n" +
            "  \"аналітичне мислення\" / \"analytical thinking\"\n" +
            "  \"стресостійкість\" / \"stress tolerance\"\n" +
            "  Education tokens (\"вища освіта\") → education_required field, not skill.\n" +
            "  These NEVER go into must_have_skills or nice_to_have_skills.",

        FullWorkedExample:
            "RAW VACANCY (Marketing example — Ukrainian performance marketing JD):\n" +
            "  Title: Performance Marketing Manager\n" +
            "  Description:\n" +
            "    Шукаємо Performance Marketing Manager у e-commerce проект.\n" +
            "    Чим будеш займатись:\n" +
            "      - Запускати кампанії у Google Ads та Meta Ads\n" +
            "      - Налаштовувати GA4, Google Tag Manager та pixel tracking\n" +
            "      - Аналізувати CAC, ROAS, LTV по каналах\n" +
            "      - Проводити A/B testing креативів та landing pages\n" +
            "      - Працювати з SEO-фахівцем над органічним трафіком\n" +
            "      - Email-маркетинг через Mailchimp\n" +
            "      - Готувати щотижневу звітність у Looker Studio\n" +
            "    Вимоги:\n" +
            "      - 2+ роки досвіду performance marketing\n" +
            "      - Знання GA4, Google Ads, Meta Ads на впевненому рівні\n" +
            "      - Розуміння unit economics та юніт-економіки\n" +
            "      - Англійська Upper-Intermediate\n" +
            "      - Аналітичне мислення (це для нас критично)\n" +
            "    Буде плюсом:\n" +
            "      - Досвід з TikTok Ads\n" +
            "      - Hotjar для аналізу поведінки\n\n" +

            "IDEAL VacancyAnalysis JSON:\n" +
            "{\n" +
            "  \"vacancy_id\": \"<runtime>\",\n" +
            "  \"source_language\": \"uk\",\n" +
            "  \"model_version\": \"vac_v4+marketing_v1\",\n" +
            "  \"role_title\": { \"en\": \"Performance Marketing Manager\", \"uk\": \"Performance Marketing Manager\" },\n" +
            "  \"seniority_required\": \"middle\",\n" +
            "  \"must_have_skills\": [\"performance marketing\", \"Google Ads\", " +
                "\"Meta Ads\", \"GA4\", \"Google Tag Manager\", \"pixel tracking\", " +
                "\"CAC\", \"ROAS\", \"LTV\", \"A/B testing\", \"landing pages\", " +
                "\"SEO\", \"email marketing\", \"Mailchimp\", \"Looker Studio\", " +
                "\"unit economics\"],\n" +
            "  \"nice_to_have_skills\": [\"TikTok Ads\", \"Hotjar\"],\n" +
            "  \"min_years_experience\": 2,\n" +
            "  \"english_required\": \"B2\",\n" +
            "  \"domain_context\": { \"en\": \"e-commerce\", \"uk\": \"e-commerce\" }\n" +
            "}\n\n" +

            "EXTRACTION LOGIC for Marketing-domain prose:\n" +
            "  - Activity bullet + named tool/methodology literal → extract\n" +
            "    \"Запускати кампанії у Google Ads та Meta Ads\" → BOTH literal tools\n" +
            "    \"Налаштовувати GA4, GTM та pixel tracking\" → all three named\n" +
            "    \"Аналізувати CAC, ROAS, LTV\" → all three metrics literal\n" +
            "    \"Проводити A/B testing креативів та landing pages\" → \"A/B testing\" + \"landing pages\"\n" +
            "  - Pure prose without literal named tokens → extract nothing\n" +
            "    e.g. \"бути на пульсі трендів\" → NOTHING (generic activity)\n\n" +

            "RESULT: 16 must_have + 2 nice_to_have = 18 named marketing skills.\n" +
            "Filter applied: \"аналітичне мислення\" dropped (universal soft trait).\n" +
            "Compare to Generic v3 output (which might have given only 4-5 skills\n" +
            "from this same prose) — the asymmetric must-vs-have skill gap is\n" +
            "what scoring needs to differentiate good fits from bad."
    );
}
