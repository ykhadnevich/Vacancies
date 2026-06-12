using System.Text;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public static class ScoringPromptCore
{
    public const string Version = "scoring_v6";

    public static string BuildReasonPrompt(
        Verdict verdict, double score, SubScores ss, ScoringEvidence ev,
        ReasonContext? ctx = null)
    {
        var sb = new StringBuilder(2200);
        sb.Append("You are writing a 2-language match explanation for a candidate vs job pairing.\n");
        sb.Append("The score, sub_scores, evidence, and context below are FACTS — do not contradict them.\n\n");


        sb.Append("CRITICAL RULES:\n");
        sb.Append("  1. Strengths MUST be drawn from `matched_skills` (verbatim) OR from sub_scores ≥ 0.85.\n");
        sb.Append("     NEVER claim a skill is a strength if it is not in `matched_skills`.\n");
        sb.Append("  2. Gaps MUST be drawn from `missing_must_haves` (verbatim) OR `triggered_anti_flags`\n");
        sb.Append("     OR sub_scores < 0.50.\n");
        sb.Append("     NEVER list a skill in gaps if it appears in `matched_skills`.\n");
        sb.Append("     NEVER list a skill in gaps if it is NOT in `missing_must_haves`.\n");
        sb.Append("  3. Hard cap ≤ 30 words per reason. Truncate if needed.\n");
        sb.Append("  4. Skill names stay Latin (\".NET\", not \"точка-нет\") in BOTH languages.\n");
        sb.Append("  5. Use the exact verdict phrase below — do not paraphrase.\n");
        sb.Append("  6. When falling back to sub_scores (because matched/missing lists are empty),\n");
        sb.Append("     ALWAYS humanize the axis name. NEVER output raw field names like\n");
        sb.Append("     \"skill_match\", \"role_intent_match\", \"domain_alignment\" in user-facing text.\n");
        sb.Append("     Use this mapping (EN | UK):\n");
        sb.Append("       skill_match       → \"skills overlap\"        | \"відповідність навичок\"\n");
        sb.Append("       seniority_match   → \"seniority level fit\"   | \"відповідність рівня\"\n");
        sb.Append("       experience_match  → \"experience depth\"      | \"глибина досвіду\"\n");
        sb.Append("       language_match    → \"language requirements\" | \"мовні вимоги\"\n");
        sb.Append("       education_match   → \"education background\"  | \"освіта\"\n");
        sb.Append("       role_intent_match → \"role alignment\"        | \"відповідність ролі\"\n");
        sb.Append("       domain_alignment  → \"domain experience\"     | \"досвід у домені\"\n");
        sb.Append("  7. PRIORITIZATION — when selecting WHICH 2 strengths and 1-2 gaps to mention:\n");
        sb.Append("     - From matched_skills, prefer in this order:\n");
        sb.Append("         (a) Named tools / brand-like tokens (\"PostgreSQL\", \".NET\", \"Kubernetes\")\n");
        sb.Append("         (b) Specific multi-word skills (\"product positioning\", \"go-to-market\")\n");
        sb.Append("         (c) Broad concepts (\"analytical thinking\", \"communication\") — last resort\n");
        sb.Append("     - From missing_must_haves, prefer in this order:\n");
        sb.Append("         (a) Brand-like named tools the candidate clearly lacks\n");
        sb.Append("         (b) Domain-specific terminology (\"1C BAS\", \"Snowflake\")\n");
        sb.Append("         (c) Broad concepts — last resort, and never as the ONLY gap mentioned\n");
        sb.Append("     - Do NOT pad the gap list. 1 critical gap > 3 vague gaps.\n");
        sb.Append("  8. ANTI-FLAGS — when `triggered_anti_flags` is non-empty, ALWAYS append the most\n");
        sb.Append("     critical one to the gaps clause using \"; <flag>\" format, even if it\n");
        sb.Append("     replaces a less critical skill gap. Anti-flags are user-actionable\n");
        sb.Append("     filters (contract type, location, military, language) and matter more\n");
        sb.Append("     than skill nuances.\n");
        sb.Append("  9. CONTEXT-LEAD PHRASING — TEMPLATE-BASED with WORD-BUDGET (scoring_v6).\n");
        sb.Append("     CANDIDATE CONTEXT provides 4 trigger flags. For each ACTIVE flag\n");
        sb.Append("     (one marked '← salient'), include the corresponding template. Templates\n");
        sb.Append("     are FORBIDDEN when their flag is inactive. Word-budget rule prevents\n");
        sb.Append("     skill-list crowding.\n\n");
        sb.Append("     PRIORITY ORDER (use this order when combining):\n");
        sb.Append("       1. Role-family mismatch  (HIGHEST — most user-actionable)\n");
        sb.Append("       2. Cross-domain          (domain transition insight)\n");
        sb.Append("       3. Overqualification     (seniority gap insight)\n");
        sb.Append("       4. Underqualification    (lowest — usually obvious from gaps)\n\n");
        sb.Append("     ── TEMPLATE 1 — ROLE-FAMILY MISMATCH ──────────────────────────\n");
        sb.Append("     ACTIVATE WHEN: target_role_aligned: NO ← salient.\n");
        sb.Append("     ESCAPE HATCH: do NOT activate if candidate_target_roles[0] and\n");
        sb.Append("       vacancy_role share the same root specialty word\n");
        sb.Append("       (Backend/Frontend/iOS/Android/Data/DevOps/QA/ML/PM/Marketing/etc.).\n");
        sb.Append("       Example: \"Senior Backend Engineer\" applying to \"Backend Developer\" →\n");
        sb.Append("       same root \"Backend\" → SKIP Template 1 even if flag active.\n");
        sb.Append("     PATTERN: \"Different role family — {candidate_target_roles[0]} → {vacancy_role}.\"\n");
        sb.Append("     EXAMPLE:\n");
        sb.Append("       target_roles=[\"iOS Engineer\"], vacancy_role=\"System Administrator\" →\n");
        sb.Append("         \"Different role family — iOS Engineer → System Administrator.\"\n\n");
        sb.Append("     ── TEMPLATE 2 — CROSS-DOMAIN ──────────────────────────────────\n");
        sb.Append("     ACTIVATE WHEN: CROSS_DOMAIN_TRANSITION: yes ← salient AND both\n");
        sb.Append("       candidate_prior_domains AND vacancy_domain are present.\n");
        sb.Append("     PATTERN: \"Cross-domain {candidate_prior_domain[0]} → {vacancy_domain}.\"\n");
        sb.Append("     FIELDS: first domain from candidate_prior_domains if comma-list.\n");
        sb.Append("     EXAMPLE:\n");
        sb.Append("       candidate_prior_domains=\"consumer apps, B2C SaaS\", vacancy_domain=\"fintech\" →\n");
        sb.Append("         \"Cross-domain consumer apps → fintech.\"\n");
        sb.Append("     FORBIDDEN: do not introduce domain CLAIM without template.\n");
        sb.Append("       Do not invent domain names absent from source fields.\n\n");
        sb.Append("     ── TEMPLATE 3 — OVERQUALIFICATION ─────────────────────────────\n");
        sb.Append("     ACTIVATE WHEN: OVERQUALIFIED_BY_YEARS field present AND ≥ 4.\n");
        sb.Append("       (Threshold 4, not 3 — avoids borderline ambiguity.)\n");
        sb.Append("     PATTERN: \"{candidate_seniority} overqualified for {vacancy_seniority} {vacancy_role_short} role.\"\n");
        sb.Append("     FIELDS: vacancy_role_short = the specialty word from vacancy_role\n");
        sb.Append("       (e.g., \"Backend\", \"iOS\", \"Data Engineer\") for specificity.\n");
        sb.Append("     EXAMPLES:\n");
        sb.Append("       candidate_seniority=senior, vacancy_seniority=junior,\n");
        sb.Append("       vacancy_role=\"Junior Data Engineer\" → vacancy_role_short=\"Data Engineer\"\n");
        sb.Append("         \"Senior overqualified for junior Data Engineer role.\"\n");
        sb.Append("       When vacancy_seniority=not_specified, drop the word:\n");
        sb.Append("         \"Senior overqualified for the Data Engineer role.\"\n");
        sb.Append("     FORBIDDEN: do not introduce overqualification CLAIM without template.\n\n");
        sb.Append("     ── TEMPLATE 4 — UNDERQUALIFICATION ────────────────────────────\n");
        sb.Append("     ACTIVATE WHEN: UNDERQUALIFIED_BY_YEARS field present AND ≥ 1.\n");
        sb.Append("     PATTERN: \"Underqualified by {N} years for {vacancy_role_short}.\"\n");
        sb.Append("     EXAMPLE: \"Underqualified by 3 years for Senior PM role.\"\n\n");
        sb.Append("     ── WORD BUDGET RULE (CRITICAL) ─────────────────────────────────\n");
        sb.Append("     Hard cap: 30 words per language reason.\n");
        sb.Append("     Reserve ≥15 words for \"[Verdict] match. Strengths: ... Gaps: ...\".\n");
        sb.Append("     Therefore: context block ≤ 15 words.\n\n");
        sb.Append("     If 1 template active: use it (typically 6-9 words).\n");
        sb.Append("     If 2 templates active AND combined ≤ 15 words: combine with \";\"\n");
        sb.Append("       in PRIORITY ORDER. Example:\n");
        sb.Append("         \"Different role family — iOS Engineer → SysAdmin;\n");
        sb.Append("          cross-domain consumer apps → enterprise.\"\n");
        sb.Append("     If 2+ templates active AND combined > 15 words: keep only TOP 1\n");
        sb.Append("       by priority. Drop lower-priority templates.\n");
        sb.Append("     If 3+ templates active: ALWAYS drop to top-2 max by priority.\n\n");
        sb.Append("     ── NO TEMPLATE ACTIVE ──────────────────────────────────────────\n");
        sb.Append("     If no flag marked '← salient', OMIT context lead entirely.\n");
        sb.Append("     Start directly with verdict word. Do not invent context to seem informative.\n\n");
        sb.Append("     ── SELF-CHECK BEFORE EMITTING ──────────────────────────────────\n");
        sb.Append("       1. Which flags are marked '← salient' in CANDIDATE CONTEXT?\n");
        sb.Append("       2. For each active flag, am I using its template with EXACT field values?\n");
        sb.Append("       3. For each inactive flag, am I avoiding its template phrasing?\n");
        sb.Append("       4. Did I check escape hatch for Template 1 (shared root word)?\n");
        sb.Append("       5. Is my context block ≤ 15 words? If not, drop to top-1 priority.\n");
        sb.Append("       6. Are templates in PRIORITY ORDER when combined?\n\n");

        sb.Append("VERDICT (mandatory — use as the opening phrase):\n");
        sb.Append($"  EN: \"{verdict.ToEnglishText()}\"\n");
        sb.Append($"  UK: \"{verdict.ToUkrainianText()}\"\n");
        sb.Append($"  composite score: {score:F2}\n\n");

        sb.Append("EVIDENCE (use ONLY these items for strengths/gaps skill names):\n");
        sb.Append($"  matched_skills (POSITIVE — these are STRENGTHS, never gaps):\n    {Join(ev.MatchedSkills)}\n");
        sb.Append($"  missing_must_haves (NEGATIVE — these are GAPS, never strengths):\n    {Join(ev.MissingMustHaves)}\n");
        sb.Append($"  triggered_anti_flags (ALWAYS surface in gaps if non-empty):\n    {Join(ev.TriggeredAntiFlags)}\n\n");

        sb.Append("SUB-SCORES (use only when evidence lists are empty):\n");
        sb.Append($"  skill_match:       {ss.SkillMatch:F2}\n");
        sb.Append($"  seniority_match:   {ss.SeniorityMatch:F2}\n");
        sb.Append($"  experience_match:  {ss.ExperienceMatch:F2}\n");
        sb.Append($"  language_match:    {ss.LanguageMatch:F2}\n");
        sb.Append($"  education_match:   {ss.EducationMatch:F2}\n");
        sb.Append($"  role_intent_match: {ss.RoleIntentMatch:F2}\n");
        sb.Append($"  domain_alignment:  {ss.DomainAlignment:F2}\n\n");


        if (ctx is not null)
        {
            sb.Append("CANDIDATE CONTEXT (use to lead with a short context phrase per rule 9):\n");
            if (ctx.CandidateYearsOfExperience is int years)
                sb.Append($"  candidate_years_of_experience:   {years}\n");
            if (ctx.VacancyRequiredYears is int req)
                sb.Append($"  vacancy_required_years:          {req}\n");
            if (ctx.OverqualifiedByYears is int over && over >= 3)
                sb.Append($"  OVERQUALIFIED_BY_YEARS:          {over}   ← salient\n");
            if (ctx.UnderqualifiedByYears is int under && under >= 1)
                sb.Append($"  UNDERQUALIFIED_BY_YEARS:         {under}  ← salient\n");
            if (!string.IsNullOrEmpty(ctx.CandidateSeniority))
                sb.Append($"  candidate_seniority:             {ctx.CandidateSeniority}\n");
            if (!string.IsNullOrEmpty(ctx.VacancySeniority))
                sb.Append($"  vacancy_seniority:               {ctx.VacancySeniority}\n");
            if (ctx.CandidateTargetRoles.Count > 0)
                sb.Append($"  candidate_target_roles:          {string.Join(", ", ctx.CandidateTargetRoles.Take(4))}\n");
            if (!string.IsNullOrEmpty(ctx.VacancyRoleEn))
                sb.Append($"  vacancy_role:                    {ctx.VacancyRoleEn}\n");
            sb.Append($"  target_role_aligned:             {(ctx.TargetRoleAligned ? "yes" : "NO — role family mismatch ← salient")}\n");
            if (ctx.CrossDomainTransition)
                sb.Append("  CROSS_DOMAIN_TRANSITION:         yes  ← salient\n");
            if (!string.IsNullOrEmpty(ctx.CandidateDomainsSummary))
                sb.Append($"  candidate_prior_domains:         {ctx.CandidateDomainsSummary}\n");
            if (!string.IsNullOrEmpty(ctx.VacancyDomain))
                sb.Append($"  vacancy_domain:                  {ctx.VacancyDomain}\n");
            sb.Append("\n");

            sb.Append("  When ONE salient flag (marked ← salient) is true, mention it briefly\n");
            sb.Append("  BEFORE the Strengths/Gaps clause. When TWO+ are true, pick the most\n");
            sb.Append("  decision-relevant for the user. When NONE are true, OMIT context lead\n");
            sb.Append("  and produce the bare \"[Verdict]. Strengths: ... Gaps: ...\" form.\n\n");
        }

        sb.Append("OUTPUT TEMPLATE (each language ≤ 30 words):\n");
        sb.Append("  Without context flag:\n");
        sb.Append("    [Verdict]. Strengths: [top 2]. Gaps: [top 1-2; anti_flag if any].\n");
        sb.Append("  With context flag:\n");
        sb.Append("    [Context phrase]. [Verdict]. Strengths: [top 2]. Gaps: [top 1-2; anti_flag if any].\n\n");

        sb.Append("EXAMPLES (showing correct evidence + context + prioritization usage):\n\n");

        sb.Append("  Example 1 — no context flag, basic evidence:\n");
        sb.Append("    matched_skills = [.NET, C#, PostgreSQL]\n");
        sb.Append("    missing_must_haves = [Kubernetes]\n");
        sb.Append("    triggered_anti_flags = []\n");
        sb.Append("  Correct EN: \"Strong match. Strengths: .NET, C# expertise. Gaps: missing Kubernetes.\"\n");
        sb.Append("  Correct UK: \"Сильна відповідність. Переваги: .NET, C#. Брак: немає Kubernetes.\"\n\n");

        sb.Append("  Example 2 — anti-flag present (rule 8: surface it):\n");
        sb.Append("    matched_skills = [Python, SQL]\n");
        sb.Append("    missing_must_haves = [PyTorch, MLOps]\n");
        sb.Append("    triggered_anti_flags = [French B1+ required]\n");
        sb.Append("  Correct EN: \"Partial match. Strengths: Python, SQL. Gaps: PyTorch, MLOps; French required.\"\n");
        sb.Append("  Correct UK: \"Часткова відповідність. Переваги: Python, SQL. Брак: PyTorch, MLOps; потрібна французька.\"\n\n");

        sb.Append("  Example 3 — OVERQUALIFIED context (rule 9 leads):\n");
        sb.Append("    candidate_years=7, vacancy_required=1, OVERQUALIFIED_BY_YEARS=6\n");
        sb.Append("    matched_skills = [SQL, Airflow, dbt]\n");
        sb.Append("    missing_must_haves = [DataForm]\n");
        sb.Append("  Correct EN: \"Senior overqualified for junior role. Strengths: Airflow, dbt expertise. Gaps: missing DataForm.\"\n");
        sb.Append("  Correct UK: \"Senior переоцінений для junior ролі. Переваги: Airflow, dbt. Брак: немає DataForm.\"\n\n");

        sb.Append("  Example 4 — CROSS-DOMAIN context (rule 9 leads):\n");
        sb.Append("    CROSS_DOMAIN_TRANSITION=yes, candidate_prior_domains=consumer apps,\n");
        sb.Append("    vacancy_domain=fintech\n");
        sb.Append("    matched_skills = [React, TypeScript]\n");
        sb.Append("    missing_must_haves = [payment systems, KYC]\n");
        sb.Append("  Correct EN: \"Cross-domain consumer→fintech transition. Strengths: React, TypeScript. Gaps: payment systems, KYC.\"\n");
        sb.Append("  Correct UK: \"Перехід consumer→fintech. Переваги: React, TypeScript. Брак: платіжні системи, KYC.\"\n\n");

        sb.Append("  Example 5 — ROLE-FAMILY mismatch (rule 9 leads — most important context):\n");
        sb.Append("    candidate_target_roles=[iOS Engineer], vacancy_role=System Administrator,\n");
        sb.Append("    target_role_aligned=NO\n");
        sb.Append("    matched_skills = []\n");
        sb.Append("    missing_must_haves = [Windows, Linux, DNS, GPO, ...]\n");
        sb.Append("  Correct EN: \"Different role family — iOS Engineer applying to SysAdmin. Gaps: Windows, Linux, networking stack.\"\n");
        sb.Append("  Correct UK: \"Інша роль — iOS Engineer на SysAdmin. Брак: Windows, Linux, мережевий стек.\"\n\n");

        sb.Append("  Example 6 — gap pruning (rule 7c: 1 critical > 3 vague):\n");
        sb.Append("    matched_skills = [React, JavaScript]\n");
        sb.Append("    missing_must_haves = [Vue 3, Composition API, SSR, SSG, ISR, GSAP,\n");
        sb.Append("                          framer-motion, RTK Query, Redux Toolkit, ...]  (many items)\n");
        sb.Append("  Correct EN: \"Partial match. Strengths: React. Gaps: Vue 3, Composition API.\"\n");
        sb.Append("  Correct UK: \"Часткова відповідність. Переваги: React. Брак: Vue 3, Composition API.\"\n");
        sb.Append("  (Do NOT list all 9 missing — pick top 2 named tools, drop the rest.)\n\n");

        sb.Append("Output a JSON object with EXACTLY two fields: reason_en, reason_uk. No commentary.\n");

        return sb.ToString();
    }

    private static string Join(IReadOnlyList<string> items) =>
        items.Count == 0 ? "(none)" : string.Join(", ", items.Take(8));
}
