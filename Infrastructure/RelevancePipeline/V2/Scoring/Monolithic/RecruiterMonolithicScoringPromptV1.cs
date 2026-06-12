using System.Text;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;

/// <summary>
/// Recruiter-facing variant of <see cref="MonolithicScoringPromptV3"/>. Identical
/// sub-score / anti-flag / confidence rubric (so the numerical ranking stays comparable
/// with the candidate-side flow), but REASON STYLE is rewritten:
/// <list type="bullet">
///   <item>Third person — "Кандидат…", "The candidate…"</item>
///   <item>Audience is the recruiter screening applications, NOT the candidate.</item>
///   <item>Worked examples reframe the same fits in recruiter voice.</item>
/// </list>
/// Distinct <see cref="Version"/> keeps the recruiter scoring rows isolated from
/// the candidate-side Mono cache.
/// </summary>
public static class RecruiterMonolithicScoringPromptV1
{
    public const string Version = "scoring_monolithic_recruiter_v1_6_source_weighting";

    public static string Build(string cvSummaryJson, string vacancyRawText)
    {
        var sb = new StringBuilder(4000);
        sb.Append("Score how well the candidate's CV matches the job. Return 7 sub-scores and an anti-flag penalty.\n");
        sb.Append("\n");
        sb.Append("ABSOLUTE RULES:\n");
        sb.Append("- Use ONLY what is literally written. If a fact is not stated, treat the axis as 'no signal' → 1.0.\n");
        sb.Append("- Use semantic understanding: \"Docker\"≈\"K8s containerization\", \"Backend Engineer\"≈\"Software Engineer\".\n");
        sb.Append("- DO NOT compute a composite score — emit only the 7 sub-scores and the penalty.\n");
        sb.Append("\n");
        sb.Append("PRECISION & DIFFERENTIATION (critical — read carefully):\n");
        sb.Append("- Emit every sub-score with TWO decimal digits of meaningful precision (e.g. 0.83, 0.74, 0.91).\n");
        sb.Append("- AVOID round defaults — 0.50, 0.70, 0.80, 0.90 should be RARE.\n");
        sb.Append("- You will be invoked on multiple candidates against the same vacancy. Two candidates that differ\n");
        sb.Append("  in ANY observable way (one extra skill, slightly different seniority signal, different years,\n");
        sb.Append("  different domain background) MUST receive sub-scores that differ by at least 0.02 on the axes\n");
        sb.Append("  where the difference lives. Identical sub-score vectors across distinct candidates are a\n");
        sb.Append("  SCORING BUG — vary by the smallest honest delta you can justify.\n");
        sb.Append("- Use the FULL [0.05, 1.00] range. Reserve 0.90+ for the genuinely best 10-15% of fits and\n");
        sb.Append("  reserve 0.20- for the bottom 5-10%.\n");
        sb.Append("- DO NOT shy away from 1.00 on a single axis. If the CV literally satisfies the axis\n");
        sb.Append("  (e.g. vacancy requires Bachelor and CV has Bachelor; vacancy needs B2 English and CV has\n");
        sb.Append("  C1; vacancy asks 5+ years and CV shows 8), the honest answer is 1.00 — not 0.95.\n");
        sb.Append("\n");
        sb.Append("Top-anchor calibration (so the BEST realistic fit can actually reach the top):\n");
        sb.Append("- skill_match     1.00 → CV has every must-have AND most nice-to-haves explicitly named.\n");
        sb.Append("                  0.95 → every must-have + a few nice-to-haves.\n");
        sb.Append("                  0.90 → every must-have, some nice-to-haves missing.\n");
        sb.Append("- seniority_match 1.00 → exact level match (Senior↔Senior, Junior↔Junior).\n");
        sb.Append("                  0.95 → vacancy says \"Senior or Lead\" and CV is solidly Senior.\n");
        sb.Append("- experience_match 1.00 → CV years ≥ required years AND directly relevant role.\n");
        sb.Append("                   0.95 → CV slightly under but in the same trajectory.\n");
        sb.Append("- role_intent_match 1.00 → CV.target_roles or recent titles literally name this role.\n");
        sb.Append("                    0.95 → tight family match (e.g. Backend ↔ Backend Engineer).\n");
        sb.Append("- domain_alignment  1.00 → same industry vertical (fintech ↔ fintech).\n");
        sb.Append("                    0.95 → directly adjacent (banking ↔ fintech for a Backend dev).\n");
        sb.Append("- language_match    1.00 → CV CEFR ≥ required CEFR.\n");
        sb.Append("- education_match   1.00 → CV degree ≥ required degree.\n");
        sb.Append("\n");
        sb.Append("It IS expected that for a genuinely top-tier match, multiple axes land at 1.00 and the\n");
        sb.Append("rest at 0.92-0.97. A pile of 0.88s with no 1.00s anywhere means you have under-rated the\n");
        sb.Append("clean signals.\n");
        sb.Append("\n");
        sb.Append("Bottom-anchor calibration (so the WORST realistic fit is honestly low, not floor-padded):\n");
        sb.Append("- skill_match     0.05 → CV has NONE of the must-haves and no semantic overlap.\n");
        sb.Append("                  0.10 → CV mentions one tangentially-related skill at best.\n");
        sb.Append("                  0.20 → CV has 1-2 must-haves out of 8-10 required.\n");
        sb.Append("- seniority_match 0.05 → vacancy explicitly says \"Senior only\" and CV is clearly internship-only.\n");
        sb.Append("                  0.10 → 3+ level gap (Trainee CV on Lead vacancy).\n");
        sb.Append("                  0.20 → 2 level gap (Junior CV on Senior vacancy).\n");
        sb.Append("- experience_match 0.05 → 0 production years vs implied/stated 5+ years required.\n");
        sb.Append("                   0.10 → 0 production years vs implied middle (3 years).\n");
        sb.Append("                   0.20 → 1 production year vs required 5.\n");
        sb.Append("- role_intent_match 0.05 → different professions (Frontend dev vs Cardiologist; PM vs Mechanic).\n");
        sb.Append("                    0.10 → adjacent profession but no signal of intent (PM CV with no PM target_roles).\n");
        sb.Append("                    0.20 → role-family overlap but title literally never matches.\n");
        sb.Append("- domain_alignment  0.05 → hard cross-domain on a domain-heavy role (healthcare ↔ iGaming for a domain-aware PM).\n");
        sb.Append("                    0.20 → cross-domain on a tech-portable role (fintech ↔ retail for backend dev).\n");
        sb.Append("- language_match    0.05 → CV CEFR 2 levels below required (A2 vs C1).\n");
        sb.Append("                    0.20 → CV CEFR 1 level below (B1 vs B2).\n");
        sb.Append("- education_match   0.20 → CV degree 1 level below required (Bachelor required, CV shows secondary only).\n");
        sb.Append("Use the FULL bottom range — 0.05, 0.07, 0.12, 0.18 are all valid scores. AVOID floor-padding\n");
        sb.Append("(everyone at 0.30 \"to be polite\"). A genuine 0.05 is honest information for the recruiter.\n");
        sb.Append("\n");

        sb.Append("# CV\n```json\n").Append(cvSummaryJson).Append("\n```\n\n");
        sb.Append("# VACANCY\n```\n").Append(TruncateForPrompt(vacancyRawText, 4000)).Append("\n```\n\n");

        sb.Append("# 7 sub-scores, each in [0.0, 1.0]\n");
        sb.Append("\n");

        sb.Append("- skill_match       : weighted fraction of vacancy must-haves covered by the CV.\n");
        sb.Append("                       Use semantic equivalence (Docker≈Kubernetes, .NET≈C#, Postgres≈PostgreSQL).\n");
        sb.Append("                       Add small bonus for nice-to-haves present.\n");
        sb.Append("                       If vacancy lists no must-haves → 1.0.\n");
        sb.Append("\n");
        sb.Append("                       SOURCE WEIGHTING — critical, recruiters care HOW the skill\n");
        sb.Append("                       shows up in the CV, not just THAT a literal term exists:\n");
        sb.Append("                         - skill in CV.domain_skills      → weight 1.0 (bullet-level evidence,\n");
        sb.Append("                                                            candidate actually did this in a role)\n");
        sb.Append("                         - skill in CV.technical_skills   → weight 0.5 (listed in Skills section\n");
        sb.Append("                                                            only, no production bullet)\n");
        sb.Append("                         - skill in CV.unverified_skills  → weight 0.0 (soft trait / personality —\n");
        sb.Append("                                                            does not count as a hireable skill match)\n");
        sb.Append("                       Compute: skill_match = Σ(weight of source) / total must-haves.\n");
        sb.Append("                       Concretely:\n");
        sb.Append("                       - Senior PM vacancy lists must-haves [\"product discovery\",\n");
        sb.Append("                         \"A/B testing\", \"roadmapping\", \"OKRs\", \"PRD writing\"].\n");
        sb.Append("                       - Candidate has \"product discovery\" only in unverified_skills,\n");
        sb.Append("                         \"roadmapping\" + \"OKRs\" in technical_skills (Skills section),\n");
        sb.Append("                         \"A/B testing\" in domain_skills (production bullet),\n");
        sb.Append("                         \"PRD writing\" absent.\n");
        sb.Append("                       - Coverage: 0.0 + 0.5 + 0.5 + 1.0 + 0.0 = 2.0 over 5 must-haves = 0.40.\n");
        sb.Append("                       This prevents a CV that lists PM methodologies in a course bullet\n");
        sb.Append("                       from scoring full-credit against a Senior PM vacancy.\n");
        sb.Append("\n");

        sb.Append("- seniority_match   : exact level=1.0; ±1 level=0.7; ±2 levels=0.3; more=0.1.\n");
        sb.Append("                       Senior CV on Junior vacancy = OVER-QUALIFIED → 0.5 max (candidate may quit).\n");
        sb.Append("                       If vacancy seniority is unspecified → 1.0.\n");
        sb.Append("\n");

        sb.Append("- experience_match  : Compare CV.experience years (count only PRODUCTION and FREELANCE durations)\n");
        sb.Append("                       against vacancy's required years.\n");
        sb.Append("                       Years must be RELEVANT to the vacancy role — a 5y Backend CV applying to\n");
        sb.Append("                       a Data Engineer role has only partially relevant years (count maybe 50-70%).\n");
        sb.Append("                       INTERNSHIP, COURSE, PET_PROJECT do NOT count toward production years.\n");
        sb.Append("                       Score = min(1.0, relevant_years / required_years).\n");
        sb.Append("                       Heavily over-qualified (3x required) → cap at 0.7 (over-qualification risk).\n");
        sb.Append("\n");
        sb.Append("                       Required-years resolution (apply IN ORDER):\n");
        sb.Append("                         1. If the vacancy literally states a number (\"5+ years\", \"від 3 років\",\n");
        sb.Append("                            \"min. 5 years\") → use that number.\n");
        sb.Append("                         2. Otherwise, if seniority_required (or the title/description literally\n");
        sb.Append("                            contains Trainee / Junior / Middle / Senior / Lead / Principal) is\n");
        sb.Append("                            known, use the implied minimum:\n");
        sb.Append("                              internship / trainee  → 0 years\n");
        sb.Append("                              junior                → 1 year\n");
        sb.Append("                              middle                → 3 years\n");
        sb.Append("                              senior                → 5 years\n");
        sb.Append("                              lead / principal      → 7 years\n");
        sb.Append("                            Then compare CV's relevant production years against this implied\n");
        sb.Append("                            minimum exactly as if it had been stated explicitly.\n");
        sb.Append("                         3. Otherwise, if the title names a professional role with no level prefix\n");
        sb.Append("                            (\"Product Manager\", \"Backend Engineer\", \"UI/UX Designer\",\n");
        sb.Append("                            \"Marketing Manager\", \"Data Analyst\"), the market default for an\n");
        sb.Append("                            unqualified hire of that role is MIDDLE → treat as 3 years required.\n");
        sb.Append("                            Recruiters posting these roles without a level still expect commercial\n");
        sb.Append("                            experience — a candidate with 0 production years scores ~0.10 here.\n");
        sb.Append("                         4. Only when the title is truly generic / unclear AND no seniority cue\n");
        sb.Append("                            exists anywhere in the text → 1.0.\n");
        sb.Append("                       Concretely:\n");
        sb.Append("                       - A CV with 0 production years applying to a Senior posting with no number →\n");
        sb.Append("                         experience_match around 0.05-0.15 (NOT 1.0).\n");
        sb.Append("                       - A CV with 0 production years applying to a plain \"Product Manager\"\n");
        sb.Append("                         posting (no Senior/Junior tag) → experience_match around 0.10-0.20\n");
        sb.Append("                         (NOT 1.0). The implied 3y middle-level bar is real signal.\n");
        sb.Append("\n");

        sb.Append("- language_match    : CEFR ladder (B2 satisfies B1, A2 fails C1).\n");
        sb.Append("                       If vacancy doesn't require English → 1.0.\n");
        sb.Append("\n");

        sb.Append("- education_match   : Bachelor on Bachelor=1.0; higher degree on lower req=1.0; lower=0.5.\n");
        sb.Append("                       If vacancy doesn't require a degree → 1.0.\n");
        sb.Append("\n");

        sb.Append("- role_intent_match : semantic closeness of vacancy role to CV.target_roles or recent experience titles.\n");
        sb.Append("                       Same profession (Backend ↔ Backend) = 1.0.\n");
        sb.Append("                       Same broad family (Software Engineer ↔ Backend Engineer) = 0.85.\n");
        sb.Append("                       Adjacent but different (Frontend ↔ Backend) = 0.4.\n");
        sb.Append("                       Different professions (Frontend dev ↔ ERP backend / PM ↔ Engineer) = 0.1.\n");
        sb.Append("                       If vacancy role title is unclear → 0.7.\n");
        sb.Append("\n");

        sb.Append("- domain_alignment  : Same industry (fintech ↔ fintech) = 1.0.\n");
        sb.Append("                       Related (banking ↔ fintech) = 0.85.\n");
        sb.Append("                       Tech-portable (fintech ↔ e-commerce for a Backend dev) = 0.7.\n");
        sb.Append("                       Hard cross-domain (fintech ↔ healthcare for a domain-heavy role) = 0.3.\n");
        sb.Append("                       If vacancy domain is unknown or 'other' → 1.0.\n");
        sb.Append("\n");
        sb.Append("                       SUB-DOMAIN ANCHORS — apply WITHIN the same broad role family:\n");
        sb.Append("                         software-PM ↔ hardware-PM        = 0.65\n");
        sb.Append("                            (Senior PM at SaaS vs Senior PM at IoT / smart-devices /\n");
        sb.Append("                             embedded company — same job title, different craft. Hardware\n");
        sb.Append("                             PMs deal with BoM cost, FCC/CE certification, firmware OTA,\n");
        sb.Append("                             supply chain — software PMs do NOT learn these by osmosis.)\n");
        sb.Append("                         B2B-PM ↔ B2C-PM                  = 0.75\n");
        sb.Append("                            (different ICPs, sales cycles, growth motions.)\n");
        sb.Append("                         fintech-PM ↔ gaming-PM           = 0.55\n");
        sb.Append("                            (different ICPs, monetisation, regulatory exposure.)\n");
        sb.Append("                         cloud-backend ↔ embedded-backend = 0.55\n");
        sb.Append("                            (same language family, different stack: web vs RTOS.)\n");
        sb.Append("                         enterprise-sales ↔ SMB-sales     = 0.75\n");
        sb.Append("                            (different deal size, sales cycle, buying committee.)\n");
        sb.Append("                       Recruiters treat these adjacent sub-domains as a ramp-up risk\n");
        sb.Append("                       (~6 months for the candidate to learn the new sub-stack). Do not\n");
        sb.Append("                       round adjacent sub-domains to 1.0 just because the role title matches.\n");
        sb.Append("\n");

        sb.Append("# anti_flag_penalty — DEFAULT 1.0\n");
        sb.Append("Be CONSERVATIVE. Only trigger on hard practical blockers that the CV literally fails.\n");
        sb.Append("Most strong technical matches deserve 1.0 — DO NOT trigger on weak or implied signals.\n");
        sb.Append("\n");
        sb.Append("TRIGGER 0.2 (HARD blocker):\n");
        sb.Append("  - Military service / mobilization: ONLY explicit phrases such as \"мобілізація\",\n");
        sb.Append("    \"призов\", \"служба за контрактом ЗСУ\", named combat units, or English equivalents.\n");
        sb.Append("  - Unpaid / volunteer / pro-bono positions.\n");
        sb.Append("  - Citizenship restriction the CV literally cannot satisfy.\n");
        sb.Append("\n");
        sb.Append("TRIGGER 0.5 (SOFT blocker — only if the CV explicitly contradicts the requirement):\n");
        sb.Append("  - \"fluent French/German/Spanish required\" AND CV.languages does NOT list that language.\n");
        sb.Append("  - Specific timezone overlap stated in the vacancy when the CV explicitly indicates a\n");
        sb.Append("    different timezone OR the time delta with Kyiv is severe (>=6 hours).\n");
        sb.Append("  - \"contract-only\" / \"freelance-only\" / \"part-time only\" only when the vacancy is\n");
        sb.Append("    unambiguous about this being the ONLY mode.\n");
        sb.Append("\n");
        sb.Append("DO NOT TRIGGER for any of the following (each of these alone leaves penalty at 1.0):\n");
        sb.Append("  - \"office in Kyiv / Lviv / any Ukrainian city\" without an explicit no-remote clause —\n");
        sb.Append("    most such vacancies are hybrid by default. Kyiv-based candidates are common.\n");
        sb.Append("  - \"hybrid\", \"flexible schedule\", \"occasional office visits\".\n");
        sb.Append("  - The vacancy being in defence-tech / military domain product (drones for ZSU, NATO\n");
        sb.Append("    contractor) when it is a NORMAL employment contract, not mobilization.\n");
        sb.Append("  - The vacancy being in banking / fintech / iGaming / DefTech / government — domain\n");
        sb.Append("    is NOT a blocker by itself; only an explicit candidate-side conflict triggers.\n");
        sb.Append("  - Candidate's current employer or industry background — we have no evidence.\n");
        sb.Append("  - \"international team\" / \"US client\" / \"global product\" — these are not timezone\n");
        sb.Append("    blockers on their own.\n");
        sb.Append("  - Any signal that requires guessing about the candidate's location, family, hobbies,\n");
        sb.Append("    political views — we have NO evidence about these.\n");
        sb.Append("\n");
        sb.Append("Combination rule: if exactly one trigger fires → use its level (0.5 or 0.2). If two or\n");
        sb.Append("more independent soft triggers fire → 0.2. Otherwise stay at 1.0.\n");
        sb.Append("List the literal phrase from the vacancy text in `triggered_anti_flags` when triggered.\n");
        sb.Append("When in doubt → 1.0.\n");
        sb.Append("\n");

        sb.Append("# confidence — self-reported certainty in [0.0, 1.0]\n");
        sb.Append("Report HOW CONFIDENT you are in the sub_scores you just produced. This is not the score\n");
        sb.Append("itself — it captures how well-grounded the score is in the inputs.\n");
        sb.Append("  1.0 → both CV and vacancy detailed, requirements explicit, overlap unambiguous.\n");
        sb.Append("  0.8 → minor ambiguity (one or two skills canonicalised; mild seniority gap).\n");
        sb.Append("  0.6 → vacancy or CV partly vague (short description, generic role title, no years).\n");
        sb.Append("  0.4 → substantial missing information — flag for human review.\n");
        sb.Append("  0.2 → almost no information to work with.\n");
        sb.Append("\n");

        // ─── RECRUITER-SPECIFIC REASON STYLE ────────────────────────────────────
        sb.Append("# REASON STYLE — write for a recruiter screening applications\n");
        sb.Append("Audience: a recruiter looking at this candidate alongside several others for the same\n");
        sb.Append("role. They want to know — quickly — whether to read the full CV and schedule a call,\n");
        sb.Append("or move on.\n");
        sb.Append("\n");
        sb.Append("Voice rules:\n");
        sb.Append("- Use THIRD person and address the recruiter. English: \"The candidate brings…\",\n");
        sb.Append("  \"Their background…\", \"They've shipped…\".\n");
        sb.Append("\n");
        sb.Append("- Ukrainian — STRICT singular third person about ONE person:\n");
        sb.Append("    USE: \"Кандидат має…\", \"У досвіді кандидата…\", \"Кандидат працював над…\",\n");
        sb.Append("         \"Рівень кандидата…\", \"Профіль кандидата…\", \"Кваліфікація показує…\",\n");
        sb.Append("         or impersonal: \"Досвід покриває…\", \"Стек збігається…\".\n");
        sb.Append("    DO NOT use \"їхній / їхня / їхнє / їхні / їх\" — these are PLURAL forms in\n");
        sb.Append("      Ukrainian. \"Їхній досвід\" applied to one candidate sounds wrong and grates\n");
        sb.Append("      on a native ear (it is not the English singular \"they\").\n");
        sb.Append("    DO NOT use \"вони\" about one candidate for the same reason.\n");
        sb.Append("    Gender unclear → prefer \"кандидат\" + masculine agreement (Ukrainian default\n");
        sb.Append("      for unknown gender — \"кандидат працював, мав, показав\") OR rephrase\n");
        sb.Append("      impersonally (\"досвід демонструє\", \"профіль свідчить\").\n");
        sb.Append("    Examples — WRONG vs RIGHT:\n");
        sb.Append("      WRONG: \"Їхній старший рівень може свідчити про надмірну кваліфікацію.\"\n");
        sb.Append("      RIGHT: \"Старший рівень кандидата може свідчити про надмірну кваліфікацію.\"\n");
        sb.Append("      WRONG: \"Їм бракує навичок продукт-менеджменту.\"\n");
        sb.Append("      RIGHT: \"Кандидату бракує навичок продукт-менеджменту.\" or\n");
        sb.Append("             \"У профілі бракує навичок продукт-менеджменту.\"\n");
        sb.Append("      WRONG: \"Їхня кваліфікація відповідає…\"\n");
        sb.Append("      RIGHT: \"Кваліфікація кандидата відповідає…\"\n");
        sb.Append("\n");
        sb.Append("- DO NOT address the candidate (\"you bring\", \"ваш досвід\", \"ви маєте\") — that voice\n");
        sb.Append("  is wrong for this audience.\n");
        sb.Append("- Conversational tone, not corporate jargon. Vary the opening across candidates — do NOT\n");
        sb.Append("  start every reason with \"Strong skills in X\" / \"Сильні навички X\".\n");
        sb.Append("  Some good openings: \"Solid fit — X experience covers the core stack, but…\",\n");
        sb.Append("  \"Доречне попадання по X, але…\", \"Background lines up with…\",\n");
        sb.Append("  \"Сильна сторона — X, проте є нюанс…\".\n");
        sb.Append("- Pick the SINGLE most-relevant strength and the SINGLE most-important gap (or hard\n");
        sb.Append("  blocker, if any). Don't enumerate everything — `matched_skills` and\n");
        sb.Append("  `missing_must_haves` already carry the full lists for the UI.\n");
        sb.Append("- Mention concrete things from the CV/vacancy by name (e.g. \"ASP.NET Core 8 + microservices\",\n");
        sb.Append("  \"5 years on Postgres\", \"US Citizenship requirement\"). Avoid generic phrases like\n");
        sb.Append("  \"good fit\", \"strong match\", \"відмінний збіг\", \"хороші навички\".\n");
        sb.Append("- Length: 25-45 words per language. Two sentences usually — one for the fit, one for\n");
        sb.Append("  the gap or blocker. If it's a clean No, one sentence is enough.\n");
        sb.Append("\n");
        sb.Append("Banned vocabulary (these expose the system):\n");
        sb.Append("- \"anti-flag\", \"triggered\", \"anti_flag_penalty\", \"sub-score\"\n");
        sb.Append("- \"анти-флаг\", \"анти-прапор\", \"спрацював\", \"тригер\", \"sub_score\"\n");
        sb.Append("- Template fragments: \"Strengths: X. Gaps: Y.\" / \"Сильні сторони: X. Прогалини: Y.\"\n");
        sb.Append("\n");
        sb.Append("When a hard blocker fires, name the actual thing in plain language:\n");
        sb.Append("  WRONG: \"Anti-flag for military service triggered.\"\n");
        sb.Append("  RIGHT: \"Hard blocker — this is a uniformed military contract, not a civilian role,\n");
        sb.Append("          and the candidate's background is firmly civilian.\"\n");
        sb.Append("  WRONG: \"Спрацював анти-флаг громадянства.\"\n");
        sb.Append("  RIGHT: \"Заблоковано вимогою громадянства США — формально кандидат не пройде\n");
        sb.Append("          перевірку, навіть зі збігом по стеку.\"\n");
        sb.Append("\n");
        sb.Append("Worked examples (recruiter voice — style reference, NOT to copy verbatim):\n");
        sb.Append("\n");
        sb.Append("Strong fit example —\n");
        sb.Append("  reason_en: \"Solid fit. Six years of .NET microservices + Postgres line up cleanly with\n");
        sb.Append("              the team's stack, and seniority matches. Mild gap on contract testing —\n");
        sb.Append("              they use Pact, the CV mentions k6 only.\"\n");
        sb.Append("  reason_uk: \"Сильний збіг. Шість років з .NET-мікросервісами та Postgres лягають у стек\n");
        sb.Append("              майже без зазорів, рівень теж. Невеликий нюанс — у них Pact для контракт-\n");
        sb.Append("              тестів, у CV згадано тільки k6.\"\n");
        sb.Append("\n");
        sb.Append("Overqualified example —\n");
        sb.Append("  reason_en: \"Technically the candidate clears this with ease — but it's a Junior role\n");
        sb.Append("              under direct supervision, and they're already mentoring middles. Likely\n");
        sb.Append("              to outgrow it in three months.\"\n");
        sb.Append("  reason_uk: \"Технічно — без проблем, але це Junior під прямим супервайзингом, а кандидат\n");
        sb.Append("              вже менторить мідлів. Імовірно, переросте позицію за квартал.\"\n");
        sb.Append("\n");
        sb.Append("Hard blocker example —\n");
        sb.Append("  reason_en: \"Stack-wise the candidate is above the bar. The deal-breaker is the mandatory\n");
        sb.Append("              relocation to Georgia plus the US-citizenship requirement — no path forward\n");
        sb.Append("              for someone based in Kyiv without US status.\"\n");
        sb.Append("  reason_uk: \"По стеку кандидат явно вище планки. Зупиняє інше — обов'язковий переїзд у\n");
        sb.Append("              Джорджію та вимога громадянства США, що не обходиться з Києва.\"\n");
        sb.Append("\n");

        sb.Append("# OUTPUT — strict JSON, no markdown, no commentary\n");
        sb.Append("{\n");
        sb.Append("  \"sub_scores\": { \"skill_match\": number, \"seniority_match\": number, \"experience_match\": number,\n");
        sb.Append("                    \"language_match\": number, \"education_match\": number, \"role_intent_match\": number,\n");
        sb.Append("                    \"domain_alignment\": number },\n");
        sb.Append("  \"anti_flag_penalty\": number,\n");
        sb.Append("  \"confidence\": number,\n");
        sb.Append("  \"matched_skills\": [strings],\n");
        sb.Append("  \"missing_must_haves\": [strings],\n");
        sb.Append("  \"triggered_anti_flags\": [strings],\n");
        sb.Append("  \"reason_en\": string (25-45 words, third person addressed to the recruiter),\n");
        sb.Append("  \"reason_uk\": string (25-45 words, third person addressed to the recruiter)\n");
        sb.Append("}\n");

        return sb.ToString();
    }

    private static string TruncateForPrompt(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text)) return "";
        return text.Length <= maxChars ? text : text[..maxChars] + "\n[…truncated]";
    }
}
