using System.Text;
using Domain.Scoring;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public static class JudgePromptCore
{


    public const string BodyVersion = "judge_body_v2_confidence";


    public static string Build(RoleFamily family)
    {
        var sb = new StringBuilder(8192);
        AppendScoreBandAnchors(sb);
        AppendPrecisionRules(sb);
        AppendConfidenceGuide(sb);
        AppendScoringPriorities(sb);
        AppendFamilyExamplesHeader(sb, family);
        AppendFamilyExamples(sb, family);
        AppendCrossFamilyAnchors(sb);
        return sb.ToString();
    }

    private static void AppendPrecisionRules(StringBuilder sb)
    {
        sb.AppendLine("=== PRECISION & DIFFERENTIATION ===");
        sb.AppendLine();
        sb.AppendLine("Emit final_score with TWO decimal digits of meaningful precision");
        sb.AppendLine("(0.83, 0.74, 0.91 — NOT 0.80, 0.75, 0.90).");
        sb.AppendLine();
        sb.AppendLine("AVOID round defaults — 0.50, 0.70, 0.80, 0.90 should be RARE.");
        sb.AppendLine("Two pairs that differ in ANY observable way (one extra match, slight");
        sb.AppendLine("seniority gap, different domain) MUST differ by at least 0.02. Identical");
        sb.AppendLine("scores across distinct pairs are a SCORING BUG — vary by the smallest");
        sb.AppendLine("honest delta you can justify.");
        sb.AppendLine();
        sb.AppendLine("Use the FULL [0.20, 0.95] range honestly. Reserve 0.85+ for the");
        sb.AppendLine("genuinely best fits and 0.25- for clear mismatches. Do NOT shy away from");
        sb.AppendLine("low scores — a genuine 0.22 is honest information.");
        sb.AppendLine();
    }

    private static void AppendConfidenceGuide(StringBuilder sb)
    {
        sb.AppendLine("=== CONFIDENCE — self-reported certainty per pair in [0.0, 1.0] ===");
        sb.AppendLine();
        sb.AppendLine("Report HOW CONFIDENT you are in the final_score for THIS pair. This is");
        sb.AppendLine("NOT the score itself — it captures how well-grounded the score is in");
        sb.AppendLine("the inputs.");
        sb.AppendLine();
        sb.AppendLine("  1.0 → both CV and vacancy are detailed, requirements explicit, the");
        sb.AppendLine("        skill / seniority / domain overlap is unambiguous either way.");
        sb.AppendLine("  0.8 → minor ambiguity (one skill canonicalised by guess, mild seniority");
        sb.AppendLine("        gap interpretation, one term semantically inferred).");
        sb.AppendLine("  0.6 → vacancy or CV partly vague (short description, generic role title,");
        sb.AppendLine("        no years specified). Score is best-guess, not certainty.");
        sb.AppendLine("  0.4 → substantial missing information: very short vacancy, no must-have");
        sb.AppendLine("        list, CV missing key sections. Flag for human review.");
        sb.AppendLine("  0.2 → almost no information to work with (1-2 sentences total).");
        sb.AppendLine();
        sb.AppendLine("Lowering confidence does NOT change final_score — only flags the result.");
        sb.AppendLine("Always emit confidence for EVERY pair.");
        sb.AppendLine();
    }

    private static void AppendScoreBandAnchors(StringBuilder sb)
    {
        sb.AppendLine("=== SCORE BAND ANCHORS (your score should LAND in the right band) ===");
        sb.AppendLine();
        sb.AppendLine("  0.85 - 0.95   EXCELLENT fit.");
        sb.AppendLine("                5+ named tools/methodologies matched, target role family,");
        sb.AppendLine("                seniority qualified or overqualified, domain transferable.");
        sb.AppendLine("                Reserved for cases where candidate CLEARLY does this work.");
        sb.AppendLine();
        sb.AppendLine("  0.75 - 0.85   STRONG fit.");
        sb.AppendLine("                3-5 named tools matched, seniority qualified, role in");
        sb.AppendLine("                target family or close adjacent, no hard gates fired.");
        sb.AppendLine();
        sb.AppendLine("  0.55 - 0.75   GOOD / PARTIAL fit.");
        sb.AppendLine("                Solid skill overlap (2-4 real matches), no hard gates,");
        sb.AppendLine("                candidate CAN do the work. Some gaps acceptable.");
        sb.AppendLine("                Includes adjacent role family with thin skill transfer.");
        sb.AppendLine();
        sb.AppendLine("  0.40 - 0.55   PARTIAL / WEAK fit.");
        sb.AppendLine("                Some real overlap (1-3 matches) but key requirements");
        sb.AppendLine("                missing, OR ONE moderate cap fired.");
        sb.AppendLine();
        sb.AppendLine("  0.25 - 0.40   WEAK fit.");
        sb.AppendLine("                Different role family with some transferable skills, OR");
        sb.AppendLine("                clear domain mismatch in domain-specific industry, OR");
        sb.AppendLine("                underqualification by 1 step.");
        sb.AppendLine();
        sb.AppendLine("  0.20 - 0.25   MISMATCH.");
        sb.AppendLine("                Multiple stacked hard caps (junior + 5yr gap + language),");
        sb.AppendLine("                OR fundamentally different work (HR for PM candidate),");
        sb.AppendLine("                OR zero relevant skills.");
        sb.AppendLine();
    }

    private static void AppendScoringPriorities(StringBuilder sb)
    {
        sb.AppendLine("=== SCORING PRIORITIES (apply in order) ===");
        sb.AppendLine();
        sb.AppendLine("0. TRUST THE LINEAR ANCHOR. When initial_score >= 0.80 AND no hard gate");
        sb.AppendLine("   fired (sub seniority_match >= 0.70, sub language_match >= 0.70, sub");
        sb.AppendLine("   role_intent_match >= 0.60), the linear formula has already accounted");
        sb.AppendLine("   for everything you'd want to check. Stay WITHIN 0.05 of initial_score.");
        sb.AppendLine("   Do NOT manufacture penalties to drop the score down to 0.70 -- that's");
        sb.AppendLine("   a calibration regression. Only override when the linear formula");
        sb.AppendLine("   genuinely missed a hard gate.");
        sb.AppendLine();
        sb.AppendLine("   PRECEDENCE: Rule #0 is OVERRIDDEN by:");
        sb.AppendLine("     (a) Hard underqualification cap (Rule #3)");
        sb.AppendLine("     (b) Language cap (Rule #4)");
        sb.AppendLine("     (c) DIFFERENT role-family cap (Rule #5, truly different families");
        sb.AppendLine("         like PM target on HR posting, Designer target on Backend posting).");
        sb.AppendLine("   When ANY of (a)/(b)/(c) applies, follow that rule's cap even if");
        sb.AppendLine("   initial_score is high. The linear formula is role-family-blind, so it");
        sb.AppendLine("   can over-score cross-family cases that Rule #5 correctly caps.");
        sb.AppendLine();
        sb.AppendLine("1. POSITIVE FRAMING. Score by what the candidate CAN DO, not by what is");
        sb.AppendLine("   missing. If matched_skills shows 5+ named tools and gates are clean,");
        sb.AppendLine("   score 0.80-0.92. If 3-4 named tools and clean gates, score 0.70-0.85.");
        sb.AppendLine("   If 2-3 named tools and clean gates, score 0.55-0.75. If matched_skills");
        sb.AppendLine("   empty AND role family different AND domain mismatch, score 0.20-0.35.");
        sb.AppendLine("   Do NOT anchor low just because missing_must_haves is long -- every JD");
        sb.AppendLine("   lists many requirements.");
        sb.AppendLine();
        sb.AppendLine("2. NO OVERQUALIFICATION PENALTY. Senior CV on Mid or Junior posting ->");
        sb.AppendLine("   full skill-fit score, no penalty. Level mismatch is a business filter,");
        sb.AppendLine("   not a skill mismatch. A Senior who clears all skill requirements of a");
        sb.AppendLine("   Mid role should score in the EXCELLENT or STRONG band.");
        sb.AppendLine();
        sb.AppendLine("3. ASYMMETRIC UNDERQUALIFICATION CAP.");
        sb.AppendLine("   If sub seniority_match below 0.30 (junior on senior, or junior on lead):");
        sb.AppendLine("     gap = 1 step  -> cap final at 0.35");
        sb.AppendLine("     gap = 2+ steps -> cap final at 0.25");
        sb.AppendLine();
        sb.AppendLine("4. LANGUAGE CAP -- only when vacancy requires above CV level.");
        sb.AppendLine("   sub language_match below 0.40 -> cap 0.40 (C2 required, CV B2 or less).");
        sb.AppendLine("   sub language_match in (0.40, 0.70] -> cap 0.55 (C1 required, CV B2).");
        sb.AppendLine();
        sb.AppendLine("5. ROLE FAMILY -- soft, not hard. Read candidate's target_roles from the");
        sb.AppendLine("   CV to determine WHICH role family this candidate is targeting, then:");
        sb.AppendLine("   - Same family (target = vacancy family) -> no role penalty.");
        sb.AppendLine("   - Adjacent within or to the family (specialization variants, related");
        sb.AppendLine("     disciplines) -> reduce by -0.05 to -0.10.");
        sb.AppendLine("     Strong adjacent fits with rich skill overlap CAN hit 0.75-0.85");
        sb.AppendLine("     (they are not capped at 0.70 -- skills win over title).");
        sb.AppendLine("   - Truly different role family (e.g. PM target on HR posting, Engineer");
        sb.AppendLine("     target on Sales posting, Designer target on Backend posting) ->");
        sb.AppendLine("     cap 0.45-0.55.");
        sb.AppendLine("   See per-family CALIBRATION EXAMPLES below for concrete anchors.");
        sb.AppendLine();
        sb.AppendLine("6. DOMAIN -- soft signal. Cross-industry where vacancy requires deep");
        sb.AppendLine("   specialised domain knowledge (pharma, FMCG NPD/SKU, iGaming LTV/ARPU,");
        sb.AppendLine("   regulatory compliance specifics): -0.05 to -0.10.");
        sb.AppendLine("   Generic tech-domain transfer (B2B SaaS -> AdTech, FinTech -> Energy):");
        sb.AppendLine("   neutral if core role-family skills transfer.");
        sb.AppendLine();
    }

    private static void AppendFamilyExamplesHeader(StringBuilder sb, RoleFamily family)
    {
        sb.AppendLine("=== CALIBRATION EXAMPLES (your role family) ===");
        sb.AppendLine();
        sb.AppendLine($"Detected primary role family for this candidate: {family}.");
        sb.AppendLine("Only this family's anchors are included to keep the prompt focused.");
        sb.AppendLine("For cross-family scenarios (vacancy outside this family) apply Rule #5");
        sb.AppendLine("and the brief cross-family anchors at the end of this block.");
        sb.AppendLine();
    }

    private static void AppendFamilyExamples(StringBuilder sb, RoleFamily family)
    {
        switch (family)
        {
            case RoleFamily.ProductManagement: AppendPmExamples(sb); break;
            case RoleFamily.Engineering:       AppendEngineeringExamples(sb); break;
            case RoleFamily.Design:            AppendDesignExamples(sb); break;
            case RoleFamily.Marketing:         AppendMarketingExamples(sb); break;
            case RoleFamily.Data:              AppendDataExamples(sb); break;
            case RoleFamily.DevOps:            AppendDevOpsExamples(sb); break;
            default:                           AppendOtherFamiliesExamples(sb); break;
        }
    }

    private static void AppendCrossFamilyAnchors(StringBuilder sb)
    {
        sb.AppendLine();
        sb.AppendLine("--- CROSS-FAMILY BRIEF ANCHORS ---");
        sb.AppendLine();
        sb.AppendLine("If the VACANCY belongs to a different family than the candidate's:");
        sb.AppendLine("  - Same niche role match (Cybersecurity, Architect, QA, Sales, PM,");
        sb.AppendLine("    ProjMgr, embedded, gamedev, blockchain) with matching stack ->");
        sb.AppendLine("    0.85-0.88 (EXCELLENT in that niche).");
        sb.AppendLine("  - Adjacent role within the wider IT umbrella (e.g. Backend -> QA,");
        sb.AppendLine("    DevOps -> Cloud Architect, Designer -> Frontend, DA -> ML) ->");
        sb.AppendLine("    0.55-0.75 (GOOD/PARTIAL, soft -0.05/-0.10 from linear).");
        sb.AppendLine("  - Truly different family (PM -> HR, Engineer -> Sales, Designer ->");
        sb.AppendLine("    Backend) -> 0.22-0.50 (cap 0.45-0.55 from Rule #5).");
        sb.AppendLine();
        sb.AppendLine("When candidate's target_roles span multiple families, pick the SINGLE");
        sb.AppendLine("anchor block above that best matches the vacancy. Do NOT average.");
    }


    private static void AppendPmExamples(StringBuilder sb)
    {
        sb.AppendLine("--- PRODUCT MANAGEMENT family ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE A -- Junior PM CV (B2, 0yr exp, tech background) on Junior PM/Ops");
        sb.AppendLine("  AdTech role: junior_required matched, B2 needed, 1yr min, matched_skills");
        sb.AppendLine("  Amplitude, SQL, GSheets, unit econ, marketing funnels (5 real matches),");
        sb.AppendLine("  no hard gates. -> SCORE 0.70 (real Junior PM fit). NOT 0.30 -- there is");
        sb.AppendLine("  meaningful skill overlap and no caps fire.");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE B -- Junior PM CV on Senior PM role (5yr exp, C1). Hard caps stack:");
        sb.AppendLine("  senior-vs-junior + language + 5yr exp gap. -> SCORE 0.22 (Mismatch).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE C -- Senior PM CV (6yr, C1, FinTech) on Senior PM iGaming role with");
        sb.AppendLine("  LTV/ARPU domain requirements. Senior matches, but iGaming domain is out");
        sb.AppendLine("  of CV scope. Some PM skills transfer. -> SCORE 0.55-0.62, NOT 0.20.");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE D -- Senior PM CV on HR Manager posting. Completely different");
        sb.AppendLine("  role family, zero PM skill use. -> SCORE 0.22 (Mismatch).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E -- Senior PM CV (6yr, C1, FinTech) on Senior PM B2B SaaS role");
        sb.AppendLine("  with 6-8 matched skills (Roadmap, OKR, A/B testing, Mixpanel, SQL,");
        sb.AppendLine("  cohort analysis, API products). Skill 1.0, seniority 1.0, exp 1.0,");
        sb.AppendLine("  role 1.0, domain transferable. NO hard gates fire. -> SCORE 0.88");
        sb.AppendLine("  (EXCELLENT fit). This is the case the linear anchor was built for --");
        sb.AppendLine("  do not under-score it.");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE F -- Senior PM CV on Mid Product Manager role with 5 matched skills");
        sb.AppendLine("  (Jira, Confluence, roadmap, A/B testing, SQL). Overqualified on level,");
        sb.AppendLine("  skill 0.6+, role 1.0, no caps. -> SCORE 0.78-0.82 (STRONG). Mid level on");
        sb.AppendLine("  the role is NOT a penalty -- the candidate can do this work.");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE G -- Senior PM CV on Project Manager role with skill 0.83 (matching");
        sb.AppendLine("  Jira, Scrum, project planning, agile, roadmap). Adjacent role family but");
        sb.AppendLine("  skills clearly transfer. -> SCORE 0.75-0.80 (Adjacent role, but strong");
        sb.AppendLine("  skill overlap moves it into STRONG band).");
        sb.AppendLine();
    }

    private static void AppendEngineeringExamples(StringBuilder sb)
    {
        sb.AppendLine("--- ENGINEERING / SOFTWARE DEVELOPMENT family ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E1 -- Senior Backend CV (8yr, Python, Django, FastAPI, PostgreSQL,");
        sb.AppendLine("  Redis, AWS, Kubernetes, microservices) on Senior Backend SaaS role with");
        sb.AppendLine("  6-8 matched named tools, clean gates. -> SCORE 0.88 (EXCELLENT fit).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E2 -- Senior Backend CV on Mid Backend role with 5 matched skills");
        sb.AppendLine("  (Python, REST, Docker, SQL, CI/CD), overqualified on level. -> SCORE");
        sb.AppendLine("  0.78-0.82 (STRONG -- overqualification is NOT a penalty).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E3 -- Senior Backend Python CV on Senior Frontend React role.");
        sb.AppendLine("  Adjacent specialization, transferable fundamentals (Git, REST, CI/CD,");
        sb.AppendLine("  testing) but specific framework/stack gap. -> SCORE 0.55-0.65 (adjacent");
        sb.AppendLine("  within Engineering family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E4 -- Same role family, DIFFERENT language/stack (e.g. Senior Java");
        sb.AppendLine("  CV on Senior Go Backend role, OR Senior React CV on Senior Vue Frontend).");
        sb.AppendLine("  Same daily work + same fundamentals; specific framework gap. A senior");
        sb.AppendLine("  engineer is expected to ramp up on new stack quickly. -> SCORE 0.70-0.80");
        sb.AppendLine("  (STRONG within same family despite stack swap).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E5 -- Senior Fullstack CV on Senior Backend-only OR Frontend-only");
        sb.AppendLine("  role with matching stack on the relevant side. Fullstack ⊇ the narrower");
        sb.AppendLine("  role, no penalty for the extra capability. -> SCORE 0.80-0.88 (STRONG/");
        sb.AppendLine("  EXCELLENT depending on skill_match).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E6 -- Senior Mobile Engineer CV (iOS / Swift / SwiftUI / UIKit) on");
        sb.AppendLine("  Senior iOS Engineer role with matching stack. -> SCORE 0.88 (EXCELLENT");
        sb.AppendLine("  within Mobile sub-family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E7 -- Senior iOS CV on Senior Android role (or vice-versa).");
        sb.AppendLine("  Adjacent within Mobile sub-family, shared mobile UX/architecture/network");
        sb.AppendLine("  patterns; platform-specific framework gap. -> SCORE 0.60-0.70 (adjacent");
        sb.AppendLine("  within Mobile).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E8 -- Senior Software Engineer CV on QA Engineer / SRE / DevOps");
        sb.AppendLine("  role (OR vice-versa: Senior QA Automation on Senior Backend role).");
        sb.AppendLine("  Bidirectionally adjacent: shared tooling (Linux, Git, scripting, Docker)");
        sb.AppendLine("  but different daily work and ownership. -> SCORE 0.45-0.55.");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE E9 -- Junior Engineer CV on Senior Lead Engineer role (5yr+ exp");
        sb.AppendLine("  gap, owns architecture/mentoring scope). -> SCORE 0.22 (Mismatch via");
        sb.AppendLine("  underqualification cap — example of Rule #3 firing).");
        sb.AppendLine();
    }

    private static void AppendDesignExamples(StringBuilder sb)
    {
        sb.AppendLine("--- DESIGN / UI / UX / PRODUCT DESIGN family ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE D1 -- Senior Product Designer CV (6yr, Figma, Design Systems, UX");
        sb.AppendLine("  Research, Prototyping, Wireframing, Sketch, user testing) on Senior");
        sb.AppendLine("  Product Designer SaaS role with matching stack, clean gates. -> SCORE");
        sb.AppendLine("  0.88 (EXCELLENT fit).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE D2 -- Senior Designer CV on Mid UI Designer role, overqualified.");
        sb.AppendLine("  -> SCORE 0.78-0.82 (STRONG, overqualification is not a penalty).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE D3 -- Senior Product Designer CV on Senior UX Researcher role.");
        sb.AppendLine("  Adjacent specialization within Design family, partial skill overlap");
        sb.AppendLine("  (user research, interviews, journey mapping) but methodology gap.");
        sb.AppendLine("  -> SCORE 0.65-0.75 (adjacent within family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE D4 -- Senior Designer CV on Marketing Manager / Frontend Developer");
        sb.AppendLine("  role. Different daily work, occasionally shared tools (Figma). -> SCORE");
        sb.AppendLine("  0.25-0.35 (different role family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE D5 -- Senior Product Designer CV on Senior Graphic Designer role.");
        sb.AppendLine("  Design family but different specialization (digital product UX vs print");
        sb.AppendLine("  / branding). -> SCORE 0.55-0.65 (adjacent but specialization gap).");
        sb.AppendLine();
    }

    private static void AppendMarketingExamples(StringBuilder sb)
    {
        sb.AppendLine("--- MARKETING / GROWTH / PMM / DIGITAL family ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE M1 -- Senior B2B Marketer CV (GTM strategy, ABM, demand gen,");
        sb.AppendLine("  HubSpot, GA4, Looker, content strategy, partner marketing) on Senior B2B");
        sb.AppendLine("  Marketing SaaS role with matching specialization. -> SCORE 0.88");
        sb.AppendLine("  (EXCELLENT fit).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE M2 -- Senior Marketing CV on Mid Content Marketing role,");
        sb.AppendLine("  overqualified. -> SCORE 0.78-0.82 (STRONG, no penalty).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE M3 -- Senior B2B Marketer CV on Senior B2C Growth role. Different");
        sb.AppendLine("  funnel approach and KPIs (LTV/CAC ratio vs viral coefficients), some");
        sb.AppendLine("  shared analytics tools. -> SCORE 0.60-0.70 (adjacent within Marketing");
        sb.AppendLine("  family, specialization gap).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE M4 -- Senior PMM CV on Sales Operations role. Adjacent revenue");
        sb.AppendLine("  function but different daily work and tooling. -> SCORE 0.55-0.65");
        sb.AppendLine("  (adjacent revenue family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE M5 -- Senior Marketing CV on Software Engineer / Backend Developer");
        sb.AppendLine("  posting. Truly different work. -> SCORE 0.20-0.30 (different role family).");
        sb.AppendLine();
    }

    private static void AppendDataExamples(StringBuilder sb)
    {
        sb.AppendLine("--- DATA / ANALYTICS / ML / DATA ENGINEERING family ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DA1 -- Senior Data Analyst CV (SQL, dbt, Looker, Tableau,");
        sb.AppendLine("  BigQuery, Python, ETL, statistical analysis) on Senior Data Analyst role");
        sb.AppendLine("  matching that stack. -> SCORE 0.88 (EXCELLENT, same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DA2 -- Senior Data Analyst CV on Mid Data Analyst role,");
        sb.AppendLine("  overqualified. -> SCORE 0.78-0.82 (STRONG, no penalty).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DA3 -- Senior Data Analyst (SQL, BI tools) CV on Senior Data");
        sb.AppendLine("  Engineer (Airflow, Spark, Kafka, dbt, Snowflake) role. Adjacent within");
        sb.AppendLine("  Data family but different stack focus (analytics vs pipelines). -> SCORE");
        sb.AppendLine("  0.55-0.65 (adjacent within Data family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DA4 -- Senior ML Engineer CV (PyTorch, MLOps, model training,");
        sb.AppendLine("  Kubeflow, feature engineering, Python, Docker) on Senior ML Engineer");
        sb.AppendLine("  role matching that stack. -> SCORE 0.88 (EXCELLENT, same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DA5 -- Senior Data Scientist (Python, statistics, A/B testing,");
        sb.AppendLine("  ML models) CV on Senior BI Analyst role. Adjacent within Data family,");
        sb.AppendLine("  partial skill overlap (SQL, dashboards, statistics) but different");
        sb.AppendLine("  daily output. -> SCORE 0.65-0.75 (adjacent within Data family, strong");
        sb.AppendLine("  fundamentals transfer).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DA6 -- Senior Data Analyst CV on Senior Backend Engineer role.");
        sb.AppendLine("  Shared language (Python, SQL) but fundamentally different work (analytics");
        sb.AppendLine("  vs services). -> SCORE 0.30-0.40 (different role family).");
        sb.AppendLine();
    }

    private static void AppendDevOpsExamples(StringBuilder sb)
    {
        sb.AppendLine("--- DEVOPS / SRE / CLOUD / INFRASTRUCTURE family ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DO1 -- Senior DevOps CV (Terraform, Kubernetes, AWS, GitLab CI,");
        sb.AppendLine("  Prometheus, Grafana, Linux, Ansible, Bash) on Senior DevOps role with");
        sb.AppendLine("  matching stack. -> SCORE 0.88 (EXCELLENT, same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DO2 -- Senior SRE CV on Mid DevOps role, overqualified.");
        sb.AppendLine("  -> SCORE 0.78-0.82 (STRONG).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DO3 -- Senior DevOps (AWS, K8s, Terraform) CV on Senior Cloud");
        sb.AppendLine("  Architect role. Adjacent within Infrastructure family, strong shared");
        sb.AppendLine("  fundamentals but architect role expects more design / cost modelling.");
        sb.AppendLine("  -> SCORE 0.70-0.80 (adjacent with strong overlap).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DO4 -- Senior DevOps CV on Senior Backend Engineer role. Shared");
        sb.AppendLine("  tooling (Docker, Git, scripting) but very different daily work (infra vs");
        sb.AppendLine("  business logic). -> SCORE 0.45-0.55 (different role family within tech).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE DO5 -- Senior DevOps CV on Senior Cybersecurity Engineer role.");
        sb.AppendLine("  Adjacent infra-ops family, partial overlap (Linux, networking, IAM,");
        sb.AppendLine("  monitoring) but different specialisation focus. -> SCORE 0.55-0.65");
        sb.AppendLine("  (adjacent infra/security).");
        sb.AppendLine();
    }

    private static void AppendOtherFamiliesExamples(StringBuilder sb)
    {
        sb.AppendLine("--- OTHER FAMILIES (Sales, HR, Project Mgmt, Cybersecurity, etc.) ---");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O1 -- Senior Cybersecurity Engineer CV (penetration testing, SIEM,");
        sb.AppendLine("  Burp Suite, OWASP, incident response, security audits, IAM) on Senior");
        sb.AppendLine("  Cybersecurity / InfoSec Engineer role. -> SCORE 0.88 (EXCELLENT).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O2 -- Senior Tech Lead / Architect (10yr, system design, scaling,");
        sb.AppendLine("  multiple stacks, mentoring) CV on Senior Solutions Architect role with");
        sb.AppendLine("  matching scope. -> SCORE 0.88 (EXCELLENT, same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O3 -- Senior Sales / Account Executive CV on Senior B2B SaaS Sales");
        sb.AppendLine("  Manager role with matching CRM stack and ICP. -> SCORE 0.88 (EXCELLENT,");
        sb.AppendLine("  same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O4 -- Senior Project Manager (PMP, PRINCE2, Jira) CV on Senior");
        sb.AppendLine("  Project Manager role same methodology family. -> SCORE 0.88 (EXCELLENT,");
        sb.AppendLine("  same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O5 -- Senior QA Engineer (manual + automation, Selenium, Cypress,");
        sb.AppendLine("  Postman, performance testing) CV on Senior QA Engineer role matching");
        sb.AppendLine("  stack. -> SCORE 0.88 (EXCELLENT, same family).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O6 -- Senior Engineering Manager / IC transition: 10yr Senior");
        sb.AppendLine("  Backend CV with clear team-lead experience on Senior Engineering Manager");
        sb.AppendLine("  role. Adjacent within engineering (technical foundation transfers), but");
        sb.AppendLine("  EM role expects people-management focus over coding. -> SCORE 0.70-0.78");
        sb.AppendLine("  (STRONG when CV shows mentoring/leadership signals; lower without them).");
        sb.AppendLine();
        sb.AppendLine("EXAMPLE O7 -- Embedded / IoT / Game Dev / Blockchain niche roles: when CV");
        sb.AppendLine("  and vacancy match the same niche (C/C++ embedded, Unity gamedev, Solidity");
        sb.AppendLine("  blockchain) with matching stack -> SCORE 0.85-0.88. Adjacent niches");
        sb.AppendLine("  within tech (C++ embedded on C++ desktop, or Unity on Unreal) -> SCORE");
        sb.AppendLine("  0.55-0.70. Different tech umbrella -> apply Rule #5 different-family cap.");
        sb.AppendLine();
    }
}
