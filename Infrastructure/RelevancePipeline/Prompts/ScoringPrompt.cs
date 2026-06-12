using Application.Common.Interfaces;
using Application.Common.Scoring;

namespace Infrastructure.RelevancePipeline.Prompts;


[Obsolete("Use IScoringPromptBuilder (V2 architecture). Will be removed after Phase 1 ships.", error: false)]
public static class ScoringPrompt
{


    public const string Version = "v23";


    public static string Build(
        string userProfileText,
        string title,
        string company,
        string description,
        RoleWeightedYears? roleYears = null)
    {
        var isStructured = userProfileText.TrimStart().StartsWith("{");

        var candidateSection = isStructured
            ? "Candidate profile (structured JSON extracted from CV):\n" + userProfileText
            : "Candidate CV (raw unstructured text — extract skills, experience level, and domain from it yourself):\n" + userProfileText;

        var cvNote = isStructured
            ? ""
            : "Note: The CV above is raw text. Carefully read it to identify the candidate's skills, real work experience (ignore courses/training), seniority level, and domain before evaluating.\n\n";

        var roleFactSection = BuildRoleFactSection(roleYears);

        return
            "You are a senior HR analyst. Evaluate how well this job matches the candidate.\n" +
            "The job description may be in Ukrainian or English — understand both.\n\n" +
            candidateSection + "\n\n" +
            cvNote +
            roleFactSection +
            "Job:\n" +
            "Title: " + title + "\n" +
            (string.IsNullOrEmpty(company) ? "" : "Company: " + company + "\n") +
            "Description: " + description + "\n\n" +
            "Respond with ONLY valid JSON (no markdown, no text outside JSON):\n" +
            "{\n" +
            "  \"score\": <integer 0-100>,\n" +
            "  \"verdict\": \"<strong_fit|good_fit|partial_fit|weak_fit>\",\n" +
            "  \"matched\": \"<key skills/tools present in BOTH candidate AND job, comma-separated, max 6, or 'none'>\",\n" +
            "  \"gaps\": [\n" +
            "    {\"item\": \"<short name of missing requirement>\", \"severity\": \"<critical|moderate|minor>\"},\n" +
            "    ...\n" +
            "  ]   (empty array [] if no gaps; max 4 entries)\n" +
            "}\n\n" +
            "GAPS FORMAT — STRICT:\n" +
            "  - Each gap MUST be an object with exactly two keys: \"item\" and \"severity\".\n" +
            "  - \"severity\" MUST be one of: critical, moderate, minor (lowercase, no other values).\n" +
            "  - The \"item\" text MUST NOT include the severity in parentheses — severity goes\n" +
            "    ONLY in the separate \"severity\" field. ❌ {\"item\":\"SQL (critical)\", ...} is WRONG.\n" +
            "    ✓ {\"item\":\"SQL\", \"severity\":\"critical\"} is CORRECT.\n" +
            "  - When 'eager to learn' / 'navchymo' applies to a skill → severity = minor (not critical).\n" +
            "  - When the job description forgives a missing requirement → severity = minor.\n" +
            "  - Empty array [] means no gaps. Do NOT use the string \"none\" — use [].\n\n" +


            "╔══════════════════════════════════════════════════════════════════╗\n" +
            "║  HARD CAPS — ABSOLUTE RULES, NO EXCEPTIONS                      ║\n" +
            "╚══════════════════════════════════════════════════════════════════╝\n" +
            "\n" +
            "STEP 1 — Identify what role and how many years the job requires.\n" +
            "  Look for: 'від X років', 'X+ years', 'мінімум X', 'at least X years', etc.\n" +
            "  If no experience required (entry-level) → required_years = 0.\n" +
            "\n" +
            "STEP 2 — Look up the candidate's weighted years for THAT SPECIFIC ROLE\n" +
            "  from the PRE-COMPUTED ROLE YEARS above. Use the exact matching category:\n" +
            "    Job requires Product Manager/PO  → use 'PM/PO weighted years'\n" +
            "    Job requires PMM/Growth Manager  → use 'PMM weighted years'\n" +
            "    Job requires Business Analyst    → use 'Business Analyst weighted years'\n" +
            "    Job requires Project Manager     → use 'Project Manager weighted years'\n" +
            "    Job requires Developer/Engineer  → use 'Developer weighted years'\n" +
            "  Adjacent roles DO NOT count: BA years ≠ PM years, PM years ≠ PMM years.\n" +
            "\n" +
            "STEP 3 — Apply the cap:\n" +
            "  • required_years = 1,  candidate_years = 0   → score MAX 62, verdict MAX partial_fit\n" +
            "  • required_years ≥ 2,  candidate_years = 0   → score MAX 30, verdict = weak_fit\n" +
            "  • required_years ≥ 2,  candidate_years < 1   → score MAX 52, verdict MAX partial_fit\n" +
            "  • required_years ≥ 3,  candidate_years ≤ 1   → score MAX 32, verdict = weak_fit\n" +
            "  • required_years ≥ 5,  candidate_years ≤ 2   → score MAX 22, verdict = weak_fit\n" +
            "  Always add: \"X+ years as [Role] (critical)\" to gaps.\n" +
            "\n" +
            "  ⚠️  No language in the job description overrides these caps:\n" +
            "    'ми цінуємо якість, а не роки' / 'компанія готова інвестувати' — caps still apply.\n" +
            "\n" +
            "STEP 4 — Additional caps:\n" +
            "  • Job requires Middle/Senior, candidate is Junior → score MAX 55, verdict MAX partial_fit\n" +
            "  • Job is Junior/Mid, candidate is Senior (overqualified) → score MAX 67\n" +
            "  • Engineering Manager / Tech Lead → score ≤ 25, weak_fit for non-engineers.\n" +
            "  • Core function mismatch (PM → Sales/Admin/Content) → weak_fit, critical gap.\n" +
            "  • Core function mismatch — score MAX 25, verdict = weak_fit. No exceptions.\n" +
            "    These roles are NOT product management regardless of title or shared keywords:\n" +
            "      Bonus/Promo Manager     → gambling bonus mechanics, wagering, promotion rules\n" +
            "      Growth Operations       → account farming, LinkedIn automation, ban bypass\n" +
            "      LiveOps Manager         → live game events, game economy, game operations\n" +
            "      FMCG Product Manager    → physical goods, packaging, supply chain, production,\n" +
            "                                servetky/napkins/household goods, food/non-food FMCG\n" +
            "      Account/Sales/Content Manager → not PM\n" +
            "    Even if description mentions Agile, hypothesis, roadmap — if PRIMARY work is above → mismatch.\n" +
            "    Even if title says 'Product Manager' — if actual duties are operations/sales → mismatch.\n\n" +


            "Junior-friendly detection — boost score when these signals appear in the job description:\n" +
            "  STRONG signals (add 8-12 to base score, can push into good_fit):\n" +
            "    'без досвіду', 'без попереднього досвіду', 'no experience required',\n" +
            "    'готові навчати', 'will train', 'entry-level', 'strong junior',\n" +
            "    'junior or', 'junior/middle', 'стажист', 'trainee', 'internship',\n" +
            "    'від 0 років', 'від 6 місяців', 'from 0 years'\n" +
            "  MODERATE signals (add 4-6 to base score):\n" +
            "    'junior', 'початківець', 'young specialist', 'young professional',\n" +
            "    'recent graduate', 'will consider candidates without'\n" +
            "  These signals indicate the company is OPEN to people without commercial experience.\n" +
            "  For career_switcher=true candidates, junior-friendly jobs are significantly more attainable.\n\n" +


            "Technical PM role — developer-turned-PM scoring:\n" +
            "  Definition: job requires or strongly values software development background\n" +
            "  (ERP, B2B SaaS, dev tools, API/platform products, fintech backend, data products,\n" +
            "   integration platforms, technical product ownership).\n\n" +
            "  If the job IS a technical PM role AND the candidate has C#/.NET, React, SQL, REST API,\n" +
            "  Docker, CI/CD, ASP.NET Core → this technical background IS the PRIMARY qualification.\n" +
            "  Do NOT treat it as 'nice to have' — it is exactly what the job requires.\n\n" +
            "  Concrete scoring for technical PM + developer-turned-PM:\n" +
            "  • Strong technical match + 0yr PM experience + junior-friendly job → score 58-68, partial_fit\n" +
            "  • Strong technical match + 0yr PM experience + 1yr requirement → score 52-62, partial_fit\n" +
            "  • Strong technical match + 0yr PM experience + 2yr requirement → score 40-52, partial_fit\n" +
            "  • NO technical match required (pure B2C, marketing PM, growth) → apply standard caps\n\n" +
            "  In gaps, list missing domain knowledge (ERP specifics, payment systems) NOT the\n" +
            "  technical skills the candidate already has. Never list 'no PM experience' as a gap\n" +
            "  when the job's primary requirement is technical background that the candidate has.\n\n" +


            "Verdict bands (score MUST fall inside the range):\n" +
            "  strong_fit  → 85-100: meets virtually all key requirements\n" +
            "  good_fit    → 65-84:  meets most requirements, gaps are learnable\n" +
            "  partial_fit → 35-64:  meets some requirements, notable gaps\n" +
            "  weak_fit    →  0-34:  fundamental mismatch\n\n" +
            "CRITICAL constraint: if ANY gap is marked (critical) → verdict CANNOT be good_fit or strong_fit.\n" +
            "  A critical gap means a fundamental missing requirement → partial_fit MAX (score ≤ 64).\n" +
            "  Exception: if the only critical gap is '1yr as [role] (critical)' — this may still be good_fit\n" +
            "  for junior-friendly jobs where the company explicitly accepts candidates without experience.\n\n" +


            "Score precision:\n" +
            "- Use precise, unique values — NOT round multiples of 5.\n" +
            "  Good: 67, 71, 74, 76, 79, 83. Bad: 70, 75, 80.\n" +
            "- Every job must receive a DIFFERENT score.\n" +
            "- Score = REAL fit of THIS job vs THIS candidate.\n" +
            "- Within good_fit (65-84): 65-69 when critical gaps, 70-76 for 1-2 moderate gaps,\n" +
            "  77-84 for mostly minor gaps or strong alignment.\n" +
            "- Scores 85+ must be rare (top ~10%). Default toward strict when uncertain.\n\n" +


            "Experience type multipliers (for assessing skills, seniority, NOT for role years):\n" +
            "  PRODUCTION=1.0x, FREELANCE=0.7x, INTERNSHIP=0.5x, PET_PROJECT=0.2x, COURSE=0.0x\n" +
            "  Junior=0-1yr weighted. Middle=2-4yrs. Senior=5+yrs.\n\n" +


            "English: if job requires B2+ and CV shows no English signals → add gap, reduce score 8-12.\n" +
            "Ukrainian employment gaps 2022-2023 are neutral (war context).\n\n" +

            "Native language handling (Ukrainian/Russian):\n" +
            "- If the candidate profile has a `languages` field → use it directly.\n" +
            "  Example: languages=[{language:Ukrainian,level:native}] → Ukrainian is NOT a gap.\n" +
            "- If no `languages` field but the CV is from a Ukrainian candidate (Ukrainian education,\n" +
            "  Ukrainian location, or CV written in Ukrainian) → assume Ukrainian and Russian are native.\n" +
            "  NEVER add 'Ukrainian (critical/moderate)' or 'Russian (critical/moderate)' as a gap\n" +
            "  for a Ukrainian candidate. This is a systematic error — they are native speakers.\n\n" +


            "Career switcher / junior without commercial experience:\n" +
            "- career_switcher=true + has_real_product_experience=false = developer transitioning to PM.\n" +
            "  This is a known, legitimate path. Many companies hire exactly this profile for technical PM.\n\n" +
            "- DO NOT add 'lack of commercial PM experience' as a gap — the experience cap already penalises this.\n" +
            "  Adding it again in gaps is double-counting. Focus on SPECIFIC missing skills instead.\n\n" +
            "- DO NOT reduce score purely because the candidate has no commercial experience.\n" +
            "  Instead, evaluate: does this candidate have the SPECIFIC skills this job requires?\n" +
            "  If yes and the job is junior-friendly → score generously (55-68 range).\n\n" +
            "- 'unverified_skills' (soft skills: analytical thinking, adaptable, etc.) → NEVER in Matched.\n" +
            "  These are self-reported with no evidence. Only domain_skills and technical_skills count.\n\n" +


            "Eager to learn / will train signal:\n" +
            "  If the job description EXPLICITLY says it will accept someone without a specific skill\n" +
            "  IF they are willing to learn — that skill is NOT a critical gap. Treat it as minor.\n" +
            "  Trigger phrases (Ukrainian + English):\n" +
            "    'eager to learn', 'willing to learn', 'or equivalent training', 'готові навчати',\n" +
            "    'або готовність навчатись', 'або бажання розвиватись', 'open to candidates without',\n" +
            "    'technical background is a plus', 'we will teach', 'навчимо', 'розглянемо без досвіду в'\n" +
            "  Example: Enapps ERP says 'ERP experience or eager to learn + technical background' →\n" +
            "    ERP experience = minor gap (explicitly forgiven). Technical background the candidate HAS.\n" +
            "    This job should score HIGHER than a job requiring ERP experience without this language.\n\n" +


            "Platform-specific tool gaps — always critical if the job EXPLICITLY requires them:\n" +
            "  These tools require months of hands-on practice and cannot be faked in an interview.\n" +
            "  Only list as a gap if the job description SPECIFICALLY MENTIONS the tool as required:\n" +
            "  Amazon: Amazon Seller Central, Amazon SEO, Helium 10, Amazon Brand Analytics\n" +
            "  Advertising: Google Ads Manager, Meta Ads Manager (hands-on campaign management)\n" +
            "  Analytics: Mixpanel, Tableau, Power BI, Looker, Firebase (when stated as primary tool)\n" +
            "  IMPORTANT: ONLY add these as gaps if the job EXPLICITLY names them. Do NOT add them\n" +
            "  speculatively — e.g., do NOT add 'Amazon Seller Central' to a POS or SaaS PM job.\n" +
            "  Generic PM skills do NOT compensate for absence of these specific platform tools.\n\n" +


            "Non-transferable domain knowledge — treat like iGaming/pharma (industry-locked):\n" +
            "  The following domains require deep prior industry experience that cannot be acquired\n" +
            "  quickly. Even junior roles in these domains expect some industry background.\n" +
            "  If job requires ANY of these and candidate has zero domain signals → score MAX 45:\n" +
            "  • Energy systems: EMS, BESS, VPP, smart grid, energy trading, SCADA, power management\n" +
            "  • Pharma/MedTech: regulatory affairs, clinical trials, CNS, oncology, drug lifecycle\n" +
            "  • Fintech/banking regulation: NBU, PSD2, SWIFT, AML compliance, core banking\n" +
            "  • Hardware/embedded: firmware, FPGA, embedded systems product management\n" +
            "  • Telecommunications: telco infrastructure, OSS/BSS, network management\n\n" +


            "Tool weight:\n" +
            "- Hard to learn (absence = moderate/critical): SQL, Python, Amplitude, Mixpanel, Tableau,\n" +
            "  Power BI, Looker, Firebase, Google Ads, Meta Ads.\n" +
            "- Easy to learn (absence = minor only): Jira, Confluence, Notion, Miro, Figma (for PM).\n\n" +


            "Matched: only skills present in BOTH job description AND candidate CV.\n" +
            "  - Must be tied to domain_skills or technical_skills (real work/study context). Max 5.\n" +
            "  - NEVER include unverified_skills (soft skills without evidence) in Matched.\n" +
            "  - NEVER include skills the candidate only listed in a 'Skills' section without usage context.\n" +
            "Gaps severity (each gap = object {\"item\":\"...\", \"severity\":\"critical|moderate|minor\"}).\n" +
            "  - 'Preferred'/'plus'/'nice to have' in job → max minor gap.\n" +
            "  - 2+ years of ROLE experience (as PM, as PMM) for a candidate with 0 → critical.\n" +
            "  - 1 year of ROLE experience (as PM, as PMM, as Project Manager, as BA) for a candidate with 0 → moderate.\n" +
            "    Rationale: 1yr role requirement is achievable with strong portfolio/skills for career-switcher.\n" +
            "  - 1+ year in a DOMAIN/INDUSTRY (fintech, gaming, iGaming, crypto, e-commerce, FMCG) → critical.\n" +
            "    Rationale: domain knowledge is non-transferable and hard to fake in an interview.\n" +
            "  - Missing hard tools (SQL, Python, Amplitude) when clearly required → critical.\n" +
            "  - Missing soft tools (Jira, Figma, Notion) → minor only.\n" +
            "Nothing matched → \"none\". No gaps → [] (empty array, not the string \"none\").\n\n" +

            "Score = REAL job fit, not keyword overlap. Be honest and precise.";
    }

    private static string BuildRoleFactSection(RoleWeightedYears? roleYears)
    {
        if (roleYears is null)
            return "";

        var r = roleYears;

        return
            "╔══════════════════════════════════════════════════════════════════╗\n" +
            "║  PRE-COMPUTED ROLE YEARS — AUTHORITATIVE, DO NOT RECALCULATE    ║\n" +
            "║  Computed from CV: COURSE=0, PRODUCTION=1.0x, FREELANCE=0.7x,  ║\n" +
            "║  INTERNSHIP=0.5x, PET_PROJECT=0.2x. Adjacent roles kept apart. ║\n" +
            "╚══════════════════════════════════════════════════════════════════╝\n" +
            $"  PM/PO weighted years          = {r.PmPo:F1}   (Product Manager, Product Owner, Head of Product)\n" +
            $"  PMM weighted years            = {r.Pmm:F1}   (Product Marketing Manager, Growth Manager)\n" +
            $"  Business Analyst weighted yrs = {r.BusinessAnalyst:F1}   (Business Analyst, Systems Analyst)\n" +
            $"  Project Manager weighted yrs  = {r.ProjectManager:F1}   (Project Manager, Program Manager)\n" +
            $"  Developer/Engineer weighted   = {r.Developer:F1}   (Software Engineer, Developer, etc.)\n" +
            $"  Data Analyst weighted yrs     = {r.DataAnalyst:F1}   (Data Analyst, Data Scientist)\n" +
            $"  Designer weighted yrs         = {r.Designer:F1}   (UX/UI Designer)\n" +
            $"  Marketing weighted yrs        = {r.Marketing:F1}   (Generic Marketing roles)\n" +
            "\n" +
            "Use these numbers in STEP 2 below. Do NOT calculate from the CV — these are final.\n\n";
    }
}
