using System.Text;
using System.Text.Json;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;


public static class MonolithicScoringPrompt
{
    public const string Version = "scoring_monolithic_v1";

    public static string Build(string cvSummaryJson, string vacancyRawText)
    {
        var sb = new StringBuilder(4000);
        sb.Append("You are a job-matching analyst. Given a candidate CV (already structured) and a raw job description (free-form text), evaluate the match and output a structured JSON response.\n\n");

        sb.Append("# CV (structured JSON):\n");
        sb.Append("```json\n");
        sb.Append(cvSummaryJson);
        sb.Append("\n```\n\n");

        sb.Append("# JOB DESCRIPTION (raw text — extract requirements yourself):\n");
        sb.Append("```\n");
        sb.Append(TruncateForPrompt(vacancyRawText, 4000));
        sb.Append("\n```\n\n");

        sb.Append("# YOUR TASK\n\n");

        sb.Append("**Step 1 — Extract requirements from the job description:**\n");
        sb.Append("  - must_have_skills (hard requirements, technologies / methodologies)\n");
        sb.Append("  - required seniority level (Junior / Middle / Senior / Lead)\n");
        sb.Append("  - minimum years of experience (integer; 0 if not stated)\n");
        sb.Append("  - English level required (A1..C2; null if not stated)\n");
        sb.Append("  - education requirement (Bachelor / Master / Other / null)\n");
        sb.Append("  - domain (e.g. fintech, healthcare, gaming; null if generic)\n");
        sb.Append("  - anti_requirements (anything that disqualifies, e.g. \"French B1+\", \"onsite only Berlin\")\n\n");

        sb.Append("**Step 2 — Compute 7 sub_scores (each 0.0..1.0):**\n");
        sb.Append("  - skill_match       : |must_have ∩ cv.technical_skills| / max(|must_have|, 1)\n");
        sb.Append("  - seniority_match   : exact=1.0, ±1 level=0.6, else=0.3\n");
        sb.Append("  - experience_match  : min(1.0, cv.years_experience / max(min_years, 1))\n");
        sb.Append("  - language_match    : CEFR ladder (B2 vs B1 ok, A2 vs C1 fail)\n");
        sb.Append("  - education_match   : degree level overlap (Bachelor on Bachelor=1.0)\n");
        sb.Append("  - role_intent_match : how closely cv.desired_role matches the offered role\n");
        sb.Append("  - domain_alignment  : 0.5 + 0.5 × cv_domain overlap with vacancy domain; 0.7 if vacancy domain null\n\n");

        sb.Append("**Step 3 — anti_flag_penalty multiplier:**\n");
        sb.Append("  - 1.0 if no anti_requirements OR all anti_requirements satisfied by CV\n");
        sb.Append("  - 0.5 if a soft anti is triggered (contract-only / city-specific / mild language gap)\n");
        sb.Append("  - 0.2 if a hard anti is triggered (B1+ foreign language CV doesn't have, onsite in unreachable country)\n\n");

        sb.Append("**Step 4 — Composite score:**\n");
        sb.Append("  score = (0.30·skill_match + 0.15·seniority_match + 0.15·experience_match + 0.10·language_match\n");
        sb.Append("          + 0.05·education_match + 0.15·role_intent_match + 0.10·domain_alignment) × anti_flag_penalty\n");
        sb.Append("  Clamp to 0..1.\n\n");

        sb.Append("**Step 5 — Evidence lists:**\n");
        sb.Append("  - matched_skills      : must_have_skills + nice_to_have_skills that ARE in cv.technical_skills or cv.domain_skills (verbatim from CV)\n");
        sb.Append("  - missing_must_haves  : must_have_skills NOT in CV\n");
        sb.Append("  - triggered_anti_flags: which anti_requirements actually fired against this CV\n\n");

        sb.Append("**Step 6 — Bilingual reason text:**\n");
        sb.Append("  Verdict thresholds: score ≥ 0.75 → \"Strong match.\" / \"Сильна відповідність.\";\n");
        sb.Append("                      score ≥ 0.50 → \"Partial match.\" / \"Часткова відповідність.\";\n");
        sb.Append("                      score ≥ 0.25 → \"Weak match.\"    / \"Слабка відповідність.\";\n");
        sb.Append("                      else         → \"Mismatch.\"      / \"Невідповідність.\".\n\n");
        sb.Append("  Template: \"[Verdict]. Strengths: [top 2 from matched_skills]. Gaps: [top 1-2 from missing_must_haves].\"\n");
        sb.Append("  Hard cap ≤ 30 words per language. Skill names stay Latin in both languages.\n");
        sb.Append("  NEVER list a skill in gaps if it's in matched_skills. NEVER claim a skill as strength if it's not in matched_skills.\n\n");

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
