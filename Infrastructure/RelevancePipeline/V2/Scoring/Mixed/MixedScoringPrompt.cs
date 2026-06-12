using System.Text;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Mixed;


public static class MixedScoringPrompt
{
    public const string Version = "scoring_mixed_rawcv_normvac_v1";

    public static string Build(string rawCvText, string normalizedVacancyJson)
    {
        var sb = new StringBuilder(8000);
        sb.Append("You are a job-matching analyst. Given a candidate's RAW CV text and a structured (already normalized) vacancy, evaluate the match and output a structured JSON response.\n\n");

        sb.Append("# CV (RAW free-form text — extract candidate's skills, seniority, experience yourself):\n```\n");
        sb.Append(TruncateForPrompt(rawCvText, 5000));
        sb.Append("\n```\n\n");

        sb.Append("# VACANCY (already normalized JSON — trust these fields verbatim):\n```json\n");
        sb.Append(normalizedVacancyJson);
        sb.Append("\n```\n\n");

        sb.Append("# YOUR TASK\n\n");


        sb.Append("**Step 1 — Extract candidate facts from the raw CV text.**\n\n");
        sb.Append("Carefully read the CV and identify:\n");
        sb.Append("  - technical_skills (technologies, frameworks, methodologies — Latin script always)\n");
        sb.Append("  - domain_skills (industries / business contexts: fintech, healthcare, gaming, etc.)\n");
        sb.Append("  - seniority (Junior / Middle / Senior / Lead — based on role titles, NOT total years)\n");
        sb.Append("  - years_experience (actual professional years, EXCLUDE courses / education / internships)\n");
        sb.Append("  - languages with CEFR levels (English, Ukrainian, etc.)\n");
        sb.Append("  - education (Bachelor / Master / PhD / None / not_specified)\n");
        sb.Append("  - desired_role (what the candidate is looking for next — from objective / title / bio)\n\n");
        sb.Append("Skills MUST be in LATIN script. Use canonical industry forms:\n");
        sb.Append("  \"asp.net core\" → \"ASP.NET Core\";  \"k8s\" → \"Kubernetes\";  \"postgres\" → \"PostgreSQL\";\n");
        sb.Append("  \"node\" → \"Node.js\";  \"react.js\" → \"React\";  \".НЕТ\" → \".NET\".\n\n");


        sb.Append("**Step 2 — Compute 7 sub_scores (each 0.0..1.0):**\n");
        sb.Append("  Match the candidate facts (from Step 1) against the vacancy fields (already given):\n");
        sb.Append("  - skill_match       : |vacancy.must_have_skills ∩ cv.technical_skills∪domain_skills| / max(|must_have|, 1)\n");
        sb.Append("                        Use canonical matching: \"asp.net core\" matches \".NET\"; \"C# 12\" matches \"C#\".\n");
        sb.Append("  - seniority_match   : exact=1.0, ±1 level=0.6, else=0.3\n");
        sb.Append("  - experience_match  : min(1.0, cv.years_experience / max(vacancy.min_years_experience, 1))\n");
        sb.Append("  - language_match    : CEFR ladder (cv.english B2 satisfies vacancy.english_required B1)\n");
        sb.Append("  - education_match   : degree ladder (Bachelor on Bachelor=1.0)\n");
        sb.Append("  - role_intent_match : closeness of cv.desired_role to vacancy.role_title.en\n");
        sb.Append("  - domain_alignment  : 0.5 + 0.5 × overlap; 0.7 if vacancy.domain_context null\n\n");

        sb.Append("**Step 3 — anti_flag_penalty multiplier:**\n");
        sb.Append("  - 1.0 if no vacancy.anti_requirements OR all satisfied by CV\n");
        sb.Append("  - 0.5 if soft anti triggered (contract-only / city-specific / mild language gap)\n");
        sb.Append("  - 0.2 if hard anti triggered (B1+ foreign language CV lacks, unreachable onsite)\n\n");

        sb.Append("**Step 4 — Composite score:**\n");
        sb.Append("  score = (0.30·skill + 0.15·seniority + 0.15·experience + 0.10·language\n");
        sb.Append("          + 0.05·education + 0.15·role_intent + 0.10·domain) × anti_flag_penalty\n");
        sb.Append("  Clamp to 0..1.\n\n");

        sb.Append("**Step 5 — Evidence lists:**\n");
        sb.Append("  - matched_skills      : vacancy.must_have + nice_to_have that ARE in extracted CV (verbatim names from VACANCY)\n");
        sb.Append("  - missing_must_haves  : vacancy.must_have_skills NOT in extracted CV\n");
        sb.Append("  - triggered_anti_flags: which anti_requirements fired against this CV\n\n");

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
