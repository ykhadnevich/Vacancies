using System.Text;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;


public static class MonolithicScoringPromptV2
{
    public const string Version = "scoring_monolithic_v2";

    public static string Build(string cvSummaryJson, string vacancyRawText)
    {
        var sb = new StringBuilder(8000);
        sb.Append("You are a job-matching analyst. Given a candidate CV (already structured) and a raw job description (free-form text), evaluate the match and output a structured JSON response.\n\n");

        sb.Append("# CV (structured JSON):\n```json\n");
        sb.Append(cvSummaryJson);
        sb.Append("\n```\n\n");

        sb.Append("# JOB DESCRIPTION (raw text — extract requirements yourself):\n```\n");
        sb.Append(TruncateForPrompt(vacancyRawText, 4000));
        sb.Append("\n```\n\n");

        sb.Append("# YOUR TASK\n\n");


        sb.Append("**Step 1 — Extract structured requirements from the job description.**\n\n");

        sb.Append("Follow these rules exactly (procedure trained over 12 prompt-eval iterations):\n\n");

        sb.Append("A. ROLE_TITLE\n");
        sb.Append("  - role_title.en: CANONICAL English title (normalized form).\n");
        sb.Append("    RULE 6.1 — drop generic \"Engineer\" suffix when redundant:\n");
        sb.Append("      \".NET Engineer\"   → \".NET Developer\"\n");
        sb.Append("      \"DevOps Engineer\" → \"DevOps\"\n");
        sb.Append("      \"iOS Engineer\"    → \"iOS Developer\"\n");
        sb.Append("      \"Backend Engineer\" → \"Backend Developer\"\n");
        sb.Append("    KEEP \"Engineer\" when specialty alone isn't a role:\n");
        sb.Append("      \"ML Engineer\" → \"ML Engineer\";  \"Data Engineer\" stays;  \"Security Engineer\" stays\n");
        sb.Append("    KEEP compound roles: \"Engineering Manager\" stays;  \"Head of Engineering\" stays\n\n");

        sb.Append("B. SKILLS — must_have vs nice_to_have, two disjoint arrays\n");
        sb.Append("  - must_have_skills: hard requirements (markers: \"required\", \"must have\", \"обов'язково\",\n");
        sb.Append("    \"досвід роботи з\", core stack mentioned without qualifier).\n");
        sb.Append("  - nice_to_have_skills: soft preferences (markers: \"plus\", \"nice to have\",\n");
        sb.Append("    \"буде перевагою\", \"бажано\", \"familiarity with\").\n");
        sb.Append("  - Each skill goes into ONE array, never both.\n");
        sb.Append("  - Filler stripping — drop verb / connector before the term:\n");
        sb.Append("      \"Досвід роботи з Linux\" → \"Linux\"\n");
        sb.Append("      \"Знання Python\"         → \"Python\"\n");
        sb.Append("      \"Experience with React\" → \"React\"\n");
        sb.Append("      \"Hands-on knowledge of AWS\" → \"AWS\"\n");
        sb.Append("  - Skills MUST be in LATIN script even when source is Ukrainian. \".NET\" stays \".NET\"\n");
        sb.Append("    in EVERY output language — NEVER transliterate to \".НЕТ\" / \"точка-нет\".\n");
        sb.Append("  - Use canonical industry forms: \"asp.net core\" → \"ASP.NET Core\", \"k8s\" → \"Kubernetes\",\n");
        sb.Append("    \"postgres\" → \"PostgreSQL\", \"node\" → \"Node.js\", \"react.js\" → \"React\".\n");
        sb.Append("  - FILTER OUT soft skills (those go elsewhere): teamwork, leadership, communication.\n");
        sb.Append("    Keep only technical/methodology skills in must_have/nice_to_have.\n\n");

        sb.Append("C. NUMERIC & CATEGORICAL — EXPLICIT-ONLY (do NOT infer from seniority/role)\n");
        sb.Append("  - min_years_experience: integer or null. Extract ONLY when text literally states\n");
        sb.Append("    \"X+ years\" / \"від X років\" / \"min. X years\". Range \"5-7\" → 5. Fractional \"2.5+\" → 3.\n");
        sb.Append("    DO NOT infer from \"Senior implies 5 years\". Not stated → null.\n");
        sb.Append("  - english_required: A1..C2 / \"native\" / null. EXPLICIT-ONLY.\n");
        sb.Append("    Mapping: \"Pre-Intermediate\" → \"A2\"; \"Intermediate\" → \"B1\";\n");
        sb.Append("    \"Upper-Intermediate\" → \"B2\"; \"Advanced\"/\"Fluent\" → \"C1\";\n");
        sb.Append("    \"Proficient\" → \"C2\"; \"Native\"/\"Носій\" → \"native\".\n");
        sb.Append("    DO NOT infer from \"international team\". Not stated → null.\n");
        sb.Append("  - education_required: \"bachelor\" / \"master\" / \"phd\" / \"none\" / null.\n");
        sb.Append("    \"вища освіта\" → \"bachelor\". \"Освіта не є визначальною\" → \"none\".\n\n");

        sb.Append("D. DOMAIN (pick EXACTLY ONE canonical tag from controlled vocab)\n");
        sb.Append("  Controlled vocabulary: fintech, iGaming, e-commerce, retail, healthcare, edtech,\n");
        sb.Append("  telecom, logistics, government, DefTech, automotive, media, consulting, crypto,\n");
        sb.Append("  B2B SaaS, consumer apps, banking, insurance, HR tech, MarTech, AdTech, agritech,\n");
        sb.Append("  energy, gaming, travel, other.\n");
        sb.Append("  Disambiguation:\n");
        sb.Append("    bank/payments → banking OR fintech (bank entity → banking; payments product → fintech)\n");
        sb.Append("    casino/gambling → iGaming (NEVER gaming)\n");
        sb.Append("    mobile game studio → gaming\n");
        sb.Append("    car-trading / auto marketplace → automotive\n");
        sb.Append("    drone / military → DefTech\n");
        sb.Append("    state enterprise / Дія / Укрпошта → government\n");
        sb.Append("    crypto wallet / web3 → crypto (NOT fintech)\n\n");

        sb.Append("E. ANTI_REQUIREMENTS — explicit exclusion criteria\n");
        sb.Append("    Onsite-only when remote would fit; foreign language fluency (French/German/Spanish);\n");
        sb.Append("    citizenship/location restrictions; contract-only positions;\n");
        sb.Append("    domain hard-excludes (\"no agencies\", \"no consulting background\");\n");
        sb.Append("    volunteer/unpaid positions.\n\n");


        sb.Append("**Step 2 — Compute 7 sub_scores (each 0.0..1.0):**\n");
        sb.Append("  - skill_match       : |must_have ∩ cv.technical_skills∪domain_skills| / max(|must_have|, 1)\n");
        sb.Append("                        Use canonical matching: \"asp.net core\" matches \".NET\"; \"C# 12\" matches \"C#\".\n");
        sb.Append("  - seniority_match   : exact=1.0, ±1 level=0.6, else=0.3\n");
        sb.Append("  - experience_match  : min(1.0, cv.years_experience / max(min_years, 1))\n");
        sb.Append("  - language_match    : CEFR ladder (B2 satisfies B1; A2 doesn't satisfy C1)\n");
        sb.Append("  - education_match   : degree level overlap (Bachelor on Bachelor=1.0)\n");
        sb.Append("  - role_intent_match : closeness of cv.desired_role to the offered role\n");
        sb.Append("  - domain_alignment  : 0.5 + 0.5 × cv_domain overlap; 0.7 if vacancy domain null\n\n");

        sb.Append("**Step 3 — anti_flag_penalty multiplier:**\n");
        sb.Append("  - 1.0 if no anti_requirements OR all satisfied by CV\n");
        sb.Append("  - 0.5 if soft anti triggered (contract-only / city-specific / mild language gap)\n");
        sb.Append("  - 0.2 if hard anti triggered (B1+ foreign language CV lacks, unreachable onsite)\n\n");

        sb.Append("**Step 4 — Composite score:**\n");
        sb.Append("  score = (0.30·skill + 0.15·seniority + 0.15·experience + 0.10·language\n");
        sb.Append("          + 0.05·education + 0.15·role_intent + 0.10·domain) × anti_flag_penalty\n");
        sb.Append("  Clamp to 0..1.\n\n");

        sb.Append("**Step 5 — Evidence lists:**\n");
        sb.Append("  - matched_skills      : must_have_skills + nice_to_have_skills that ARE in cv (verbatim)\n");
        sb.Append("  - missing_must_haves  : must_have_skills NOT in CV\n");
        sb.Append("  - triggered_anti_flags: which anti_requirements actually fired against this CV\n\n");

        sb.Append("**Step 6 — Bilingual reason text:**\n");
        sb.Append("  Verdict thresholds: ≥0.75 → \"Strong match.\" / \"Сильна відповідність.\"\n");
        sb.Append("                       ≥0.50 → \"Partial match.\" / \"Часткова відповідність.\"\n");
        sb.Append("                       ≥0.25 → \"Weak match.\"    / \"Слабка відповідність.\"\n");
        sb.Append("                       else  → \"Mismatch.\"      / \"Невідповідність.\"\n");
        sb.Append("  Template: \"[Verdict]. Strengths: [top 2 from matched_skills]. Gaps: [top 1-2 from missing_must_haves].\"\n");
        sb.Append("  Hard cap ≤ 30 words per language. Skill names stay Latin in both languages.\n");
        sb.Append("  NEVER list a skill in gaps if it's in matched_skills.\n");
        sb.Append("  NEVER claim a skill as strength if it's not in matched_skills.\n\n");

        sb.Append("# OUTPUT\n");
        sb.Append("Return a single JSON object with these fields (no commentary):\n");
        sb.Append("  score, sub_scores { skill_match, seniority_match, experience_match, language_match,\n");
        sb.Append("                      education_match, role_intent_match, domain_alignment },\n");
        sb.Append("  anti_flag_penalty, matched_skills (array), missing_must_haves (array),\n");
        sb.Append("  triggered_anti_flags (array), reason_en (string), reason_uk (string).\n");

        return sb.ToString();
    }

    private static string TruncateForPrompt(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxChars ? text : text[..maxChars] + "\n[…truncated]";
    }
}
