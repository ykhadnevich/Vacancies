using Application.Common.VacancyNormalization;

namespace Infrastructure.RelevancePipeline.V2.VacancyNormalization;


public static class VacancyNormalizationPromptCore
{


    public const string Version = "v4_1_confidence";

    public static string Build(string vacancyRawText, VacancyNormalizationSlots slots) =>
        "You are a vacancy parsing expert. Extract a structured job posting analysis from the\n" +
        "vacancy description below. Follow the procedure exactly. The output JSON schema is\n" +
        "enforced by the runtime — your job is to fill it with correct values.\n\n" +

        "Vacancy text:\n" +
        vacancyRawText + "\n\n" +
        BuildInstructionsBody(slots);


    public static string BuildInstructionsBody(VacancyNormalizationSlots slots) =>
        "═══════════════════════════════════════════════════════════════\n" +
        "A. LANGUAGE & ROLE_TITLE\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "1. source_language: detect via character mix in the description body.\n" +
        "   Cyrillic dominant (Ukrainian) → \"uk\". Latin dominant → \"en\".\n" +
        "   Roughly balanced → \"mixed\".\n\n" +

        "2. role_title.en: the CANONICAL English role title, normalized form.\n\n" +

        "   RULE 6.1 — drop generic \"Engineer\" suffix when redundant:\n" +
        "     \".NET Engineer\"        → \".NET Developer\"\n" +
        "     \"DevOps Engineer\"     → \"DevOps\"\n" +
        "     \"iOS Engineer\"        → \"iOS Developer\"\n" +
        "     \"Backend Engineer\"    → \"Backend Developer\"\n" +
        "   KEEP \"Engineer\" when the specialty alone is not a role:\n" +
        "     \"ML Engineer\"          → \"ML Engineer\"  (ML alone is not a role)\n" +
        "     \"Data Engineer\"        → \"Data Engineer\"\n" +
        "     \"Security Engineer\"    → \"Security Engineer\"\n" +
        "   KEEP compound roles where \"Engineering\" denotes scope:\n" +
        "     \"Engineering Manager\"  → \"Engineering Manager\"\n" +
        "     \"Head of Engineering\"  → \"Head of Engineering\"\n\n" +

        "3. role_title.uk: proper Ukrainian rendering. Skill names stay LATIN even\n" +
        "   inside the UK string ('.NET' is '.NET' in both languages — NEVER '.НЕТ').\n\n" +

        "4. role_title_raw: preserve exactly as found in the original title field.\n" +
        "   No cleanup, no normalization. Typos / emoji / trailing IDs are kept.\n\n" +


        slots.SeniorityKeywords + "\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "B.0  LITERAL-ONLY EXTRACTION RULE (CRITICAL — read before B)\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "Every skill in must_have_skills / nice_to_have_skills MUST appear:\n" +
        "  (i)  as a literal token in the vacancy text (case-insensitive,\n" +
        "       trivial whitespace / punctuation variations allowed), OR\n" +
        "  (ii) as an explicit bullet / section item / named requirement.\n\n" +

        "NO PARAPHRASING. NO INFERENCE. NO ADJACENT-TOOL EXPANSION.\n" +
        "Do not derive named concepts from activity prose. Do not list tools\n" +
        "that \"would normally accompany\" what is mentioned. Do not split a\n" +
        "described activity into multiple methodology terms.\n\n" +

        "CORRECT — extraction tracks the literal text:\n" +
        "  Text: \"Працювали з Snowflake та Airflow\"\n" +
        "   →    must_have_skills: [\"Snowflake\", \"Airflow\"]\n" +
        "  Text: \"Створення моделей даних за допомогою DataForm\"\n" +
        "   →    must_have_skills: [\"DataForm\"]\n" +
        "  Text: \"Знання C# та ASP.NET Core\"\n" +
        "   →    must_have_skills: [\"C#\", \"ASP.NET Core\"]\n" +
        "  Text: \"5+ years in product marketing, product strategy\"\n" +
        "   →    must_have_skills: [\"product marketing\", \"product strategy\"]\n" +
        "        (both phrases appear literally — extract as-is.)\n" +
        "  Text: \"Required: A/B testing, hypothesis validation\"\n" +
        "   →    must_have_skills: [\"A/B testing\", \"hypothesis validation\"]\n" +
        "        (both literal bullet items.)\n\n" +

        "WRONG — do NOT do any of these:\n" +
        "  Text: \"DataForm для моделей даних\"\n" +
        "   ✗    must_have_skills: [\"DWH\", \"ETL\", \"ELT\"]\n" +
        "        (none of those tokens appear; do not infer them from DataForm.)\n" +
        "  Text: \"Знання C# та ASP.NET Core\"\n" +
        "   ✗    must_have_skills: [..., \"Dependency Injection\", \"async/await\",\n" +
        "                            \"NuGet\", \"VB6\", \"VBA\"]\n" +
        "        (only C# and ASP.NET Core are mentioned; the rest are invented.)\n" +
        "  Text: \"Senior DevOps with cloud experience\"\n" +
        "   ✗    must_have_skills: [\"AWS\", \"GCP\", \"Azure\", \"Kubernetes\",\n" +
        "                            \"Terraform\", \"Ansible\", \"Nomad\"]\n" +
        "        (only \"cloud\" is mentioned — specific vendors and tools are NOT.)\n" +
        "  Text: \"Define why us, why now, for whom\"\n" +
        "   ✗    must_have_skills: [\"product positioning\", \"messaging\",\n" +
        "                            \"go-to-market strategy\"]\n" +
        "        (text describes an activity but uses none of those named\n" +
        "        concepts; DO NOT translate prose into methodology terms.)\n" +
        "  Text: \"Talk to users and buyers to understand real-world needs\"\n" +
        "   ✗    must_have_skills: [\"customer research\", \"user interviews\",\n" +
        "                            \"customer development\"]\n" +
        "        (one activity described in prose ≠ three named methodologies.)\n" +
        "  Text: \"Аналіз ринку iGaming\"\n" +
        "   ✗    must_have_skills: [\"market analysis\", \"competitor analysis\",\n" +
        "                            \"trend analysis\", \"consumer needs analysis\"]\n" +
        "        (only the literal phrase \"market analysis\" should be extracted —\n" +
        "        \"market analysis\" tracks the Ukrainian \"аналіз ринку\" closely.)\n\n" +

        "SIZE CEILING — soft guideline:\n" +
        "  Typical vacancy yields 5-20 must_have_skills. If your draft exceeds 20\n" +
        "  items, you are paraphrasing. Re-read the source and keep only literal\n" +
        "  bullets / named tokens. PM and Marketing vacancies often produce only\n" +
        "  5-10 must_haves — this is correct, not under-extraction.\n\n" +

        "EDGE CASES:\n" +
        "  - Multi-language tokens (C# = same in EN/UK; PostgreSQL = same): extract\n" +
        "    once in the canonical Latin form (see SkillCanonicalization slot).\n" +
        "  - Abbreviation expansions (\"Cascading Style Sheets\" appears, text\n" +
        "    never says \"CSS\"): extract the literal form (\"Cascading Style Sheets\").\n" +
        "    Canonicalization happens later — do not pre-expand or pre-contract.\n" +
        "  - Section headers (\"Skills:\" / \"Стек:\") are NOT skills themselves; the\n" +
        "    bullets under them are.\n" +
        "  - Years / seniority / language CEFR / location → handled by sections C/D,\n" +
        "    not skill arrays.\n\n" +

        "WHEN UNCERTAIN → DO NOT EXTRACT.\n" +
        "False \"missing\" is worse than missing a real skill: the scoring\n" +
        "calculator's ratio formula amplifies false missings into score crashes.\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "B. SKILLS\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "Skills are extracted into TWO arrays based on phrasing markers.\n\n" +


        slots.MustVsNiceMarkers + "\n\n" +

        "Each skill goes into must_have_skills OR nice_to_have_skills, never both.\n" +
        "Filler stripping — drop verb / connector words before the term:\n" +
        "  \"Досвід роботи з Linux\"     → \"Linux\"\n" +
        "  \"Знання Python\"             → \"Python\"\n" +
        "  \"Розуміння Kafka\"           → \"Kafka\"\n" +
        "  \"Experience with React\"     → \"React\"\n" +
        "  \"Hands-on knowledge of AWS\" → \"AWS\"\n\n" +

        "Skills MUST be in Latin script even when the source vacancy is Ukrainian.\n" +
        "Use canonical industry forms (see slot below). Do NOT transliterate Latin\n" +
        "into Cyrillic — \".NET\" is \".NET\" in every output language.\n\n" +


        slots.SkillCanonicalization + "\n\n" +


        slots.SoftTraitFilterGuide + "\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "C. NUMERIC & CATEGORICAL FIELDS\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "  min_years_experience — integer or null. EXPLICIT-ONLY.\n" +
        "    Only extract when the description LITERALLY states a years-of-\n" +
        "    experience requirement in phrases like \"5+ years\", \"від 3 років\",\n" +
        "    \"3+ years of commercial experience\", \"min. 5 years\". Lower bound.\n" +
        "    Fractional \"2.5+\" → round UP (3). Range \"5-7\" → 5.\n" +
        "    DO NOT infer from seniority (\"Senior role implies 5 years\" — NO).\n" +
        "    DO NOT infer from \"long-term\", \"established\", \"hands-on\" wording.\n" +
        "    When not LITERALLY stated → null.\n\n" +

        "  education_required — categorical. Mapping:\n" +
        "    \"бакалавр\" / \"Bachelor's\"               → \"bachelor\"\n" +
        "    \"вища освіта\" (UA generic higher edu)   → \"bachelor\"\n" +
        "    \"магістр\" / \"Master's\"                  → \"master\"\n" +
        "    \"PhD\" / \"кандидат наук\"                 → \"phd\"\n" +
        "    \"Освіта не є визначальною\"               → \"none\"\n" +
        "    Not mentioned                            → \"not_specified\"\n\n" +

        "  english_required — CEFR + \"native\" + \"not_specified\". EXPLICIT-ONLY.\n" +
        "    Only extract when CEFR letter (\"B1\", \"C1\") or a named level\n" +
        "    (\"Intermediate\", \"Upper-Intermediate\", \"Fluent\", \"Pre-Intermediate\",\n" +
        "    \"Вільно\", \"Носій\", \"Pre-Intermediate+\") appears verbatim.\n" +
        "    Mapping:\n" +
        "      \"Pre-Intermediate\"      → \"A2\"\n" +
        "      \"Intermediate\"          → \"B1\"\n" +
        "      \"Upper-Intermediate\"    → \"B2\"\n" +
        "      \"Advanced\" / \"Fluent\"   → \"C1\"\n" +
        "      \"Proficient\"            → \"C2\"\n" +
        "      \"Native\" / \"Носій\"      → \"native\"\n" +
        "    DO NOT infer from \"international team\" or \"US client\" wording.\n" +
        "    Not literally mentioned → \"not_specified\".\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "D. LOCATION\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "  location.city_en — English transliteration (Wikipedia form):\n" +
        "    Київ → Kyiv;  Львів → Lviv;  Харків → Kharkiv;  Дніпро → Dnipro;\n" +
        "    Одеса → Odesa;  Запоріжжя → Zaporizhzhia;  Краків → Kraków;\n" +
        "    Варшава → Warsaw.\n" +
        "  location.city_uk — original Cyrillic form.\n" +
        "  Strip district / street details — only the city name remains.\n" +
        "  If city is unknown in the posting → both city_en and city_uk null.\n\n" +

        "  location.remote — true if text mentions \"віддалено\", \"remote\",\n" +
        "    \"work from anywhere\", \"дистанційно\". False otherwise.\n" +
        "  location.hybrid — true if \"hybrid\", \"гібрид\", \"part-time office\".\n" +
        "  Both false → implicit onsite.\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "E. DOMAIN CONTEXT & ANTI-REQUIREMENTS\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "  domain_context.en / .uk — pick EXACTLY ONE canonical tag from the\n" +
        "    controlled vocabulary below. Output only the chosen tag in EN, and\n" +
        "    its standard UA translation. NO free prose, NO comma-separated\n" +
        "    multi-tag values (v1 baseline showed multi-tag prose scored 0.000\n" +
        "    against fixed-vocabulary judges).\n\n" +
        "    Controlled vocabulary (en → uk):\n" +
        "      \"fintech\"             → \"фінтех\"\n" +
        "      \"iGaming\"             → \"iGaming\"\n" +
        "      \"e-commerce\"          → \"e-commerce\"\n" +
        "      \"retail\"              → \"рітейл\"\n" +
        "      \"healthcare\"          → \"healthcare\"\n" +
        "      \"edtech\"              → \"edtech\"\n" +
        "      \"telecom\"             → \"телеком\"\n" +
        "      \"logistics\"           → \"логістика\"\n" +
        "      \"government\"          → \"держсектор\"\n" +
        "      \"DefTech\"             → \"DefTech\"\n" +
        "      \"automotive\"          → \"автомобільна сфера\"\n" +
        "      \"media\"               → \"медіа\"\n" +
        "      \"consulting\"          → \"консалтинг\"\n" +
        "      \"crypto\"              → \"крипто\"\n" +
        "      \"B2B SaaS\"            → \"B2B SaaS\"\n" +
        "      \"consumer apps\"       → \"consumer-застосунки\"\n" +
        "      \"banking\"             → \"банкінг\"\n" +
        "      \"insurance\"           → \"страхування\"\n" +
        "      \"HR tech\"             → \"HR tech\"\n" +
        "      \"MarTech\"             → \"MarTech\"\n" +
        "      \"AdTech\"              → \"AdTech\"\n" +
        "      \"agritech\"            → \"агротех\"\n" +
        "      \"energy\"              → \"енергетика\"\n" +
        "      \"gaming\"              → \"геймдев\"  (NOTE: gaming = entertainment game dev, NOT iGaming)\n" +
        "      \"travel\"              → \"туризм\"\n" +
        "      \"other\"               → \"інше\"  (use only when none of the above fit)\n\n" +
        "    Hierarchy hints when text touches multiple:\n" +
        "      bank/payments → \"banking\" or \"fintech\" (bank entity → banking, payments product → fintech)\n" +
        "      casino/gambling → \"iGaming\" (NEVER \"gaming\")\n" +
        "      mobile game studio → \"gaming\"\n" +
        "      car-trading / auto marketplace → \"automotive\"\n" +
        "      drone / military / DefTech → \"DefTech\"\n" +
        "      state enterprise / national gov / Дія / Укрпошта → \"government\"\n" +
        "      crypto wallet / web3 → \"crypto\" (not \"fintech\")\n\n" +

        "  anti_requirements — explicit EXCLUSION criteria from the posting:\n" +
        "    Onsite-only when remote candidate would otherwise fit\n" +
        "    Language fluency beyond English (French, German, Spanish)\n" +
        "    Citizenship / location restrictions (\"must be based in X\")\n" +
        "    Contract-only / no-permanent positions\n" +
        "    Domain hard-excludes (\"no agencies\", \"no consulting background\")\n" +
        "    Volunteer / unpaid positions (treat as anti_requirement when relevant)\n\n" +


        slots.AntiRequirementsGuide + "\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "F. FULL WORKED EXAMPLE\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +


        slots.FullWorkedExample + "\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "G. CONFIDENCE — self-reported certainty in [0.0, 1.0]\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "Report HOW CONFIDENT you are in the normalization you just produced. This is NOT\n" +
        "a quality score on the vacancy — it captures how well-grounded the extracted\n" +
        "structure is in the input text.\n\n" +
        "  1.0 → vacancy is detailed (>800 chars), requirements section is explicit,\n" +
        "        skill list is unambiguous, role_title is canonical industry form.\n" +
        "  0.8 → minor ambiguity (one skill canonicalised by guess, mild seniority cue\n" +
        "        from desc rather than title). Overall structure is clear.\n" +
        "  0.6 → vacancy is moderately short (300-800 chars) OR uses generic prose\n" +
        "        without explicit must-have list. Best-guess extraction.\n" +
        "  0.4 → substantial missing information: very short body (<300 chars), no\n" +
        "        requirements section, role title vague. Flag for human review.\n" +
        "  0.2 → almost no information to work with (1-2 sentences total, marketing\n" +
        "        blurb, no concrete signals).\n\n" +
        "Lowering confidence does NOT change the extracted fields — only flags the\n" +
        "result as uncertain for downstream consumers (matchers, recruiters, UI).\n" +
        "Output as top-level field \"confidence\": number.\n";
}
