using Application.Common.Interfaces;
using Application.Common.Scoring;
using System.Globalization;
using System.Text;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public static class PromptCore
{


    public const string Version = "v1";


    public static string BuildDefault(SlotId slotId, ScoringPromptContext ctx)
    {


        if (slotId == SlotId.Header)             return BuildHeader(ctx);
        if (slotId == SlotId.OutputSpec)         return BuildOutputSpec();
        if (slotId == SlotId.PreComputedYears)   return BuildPreComputedYears(ctx);

        if (slotId == SlotId.HardCapsStep1)      return HardCapsStep1;
        if (slotId == SlotId.HardCapsStep2Map)   return HardCapsStep2MapIntro;
        if (slotId == SlotId.HardCapsStep3)      return HardCapsStep3;

        if (slotId == SlotId.MidSeniorJuniorCap) return MidSeniorJuniorCap;
        if (slotId == SlotId.OverqualifiedCap)   return OverqualifiedCap;
        if (slotId == SlotId.EngineeringMgrRule) return EngineeringMgrRule;
        if (slotId == SlotId.CoreFunctionMismatch) return CoreFunctionMismatchPattern;

        if (slotId == SlotId.MismatchExamples)   return string.Empty;
        if (slotId == SlotId.JuniorFriendly)     return JuniorFriendly;
        if (slotId == SlotId.FamilyBoost)        return string.Empty;

        if (slotId == SlotId.VerdictBands)       return VerdictBands;
        if (slotId == SlotId.ScorePrecision)     return ScorePrecision;
        if (slotId == SlotId.ExperienceMultipliers) return ExperienceMultipliers;

        if (slotId == SlotId.LanguageHandling)   return LanguageHandling;
        if (slotId == SlotId.CareerSwitcherGen)  return CareerSwitcherGeneral;
        if (slotId == SlotId.CareerSwitcherFam)  return string.Empty;
        if (slotId == SlotId.EagerToLearn)       return EagerToLearn;

        if (slotId == SlotId.PlatformToolsRule)  return PlatformToolsRule;
        if (slotId == SlotId.PlatformToolsList)  return string.Empty;
        if (slotId == SlotId.DomainLock)         return DomainLock;

        if (slotId == SlotId.ToolWeightMeta)     return ToolWeightMeta;
        if (slotId == SlotId.ToolWeightList)     return string.Empty;
        if (slotId == SlotId.MatchedGapsRules)   return MatchedGapsRules;

        if (slotId == SlotId.Finale)             return Finale;

        throw new ArgumentException(
            $"Unknown SlotId '{slotId}'. Add a default in PromptCore.BuildDefault " +
            $"or remove from SlotId.AllInOrder.");
    }


    private static string BuildHeader(ScoringPromptContext ctx)
    {
        var isStructured = ctx.CvText.TrimStart().StartsWith("{");

        var candidateBlock = isStructured
            ? "Candidate profile (structured JSON extracted from CV):\n" + ctx.CvText
            : "Candidate CV (raw unstructured text — extract skills, experience level, and domain from it yourself):\n" + ctx.CvText;

        var cvNote = isStructured
            ? string.Empty
            : "Note: The CV above is raw text. Carefully read it to identify the candidate's skills, real work experience (ignore courses/training), seniority level, and domain before evaluating.";

        var sb = new StringBuilder();
        sb.AppendLine("You are a senior HR analyst. Evaluate how well this job matches the candidate.");
        sb.AppendLine("The job description may be in Ukrainian or English — understand both.");
        sb.AppendLine();
        sb.AppendLine(candidateBlock);
        if (cvNote.Length > 0)
        {
            sb.AppendLine();
            sb.AppendLine(cvNote);
        }
        sb.AppendLine();
        sb.AppendLine("Job:");
        sb.AppendLine("Title: " + ctx.JobTitle);
        if (!string.IsNullOrEmpty(ctx.JobCompany))
            sb.AppendLine("Company: " + ctx.JobCompany);
        sb.Append("Description: ").Append(ctx.JobDescription);
        return sb.ToString();
    }

    private static string BuildPreComputedYears(ScoringPromptContext ctx)
    {

        if (ctx.RoleYears is null) return string.Empty;

        var r = ctx.RoleYears;
        var inv = CultureInfo.InvariantCulture;
        return
            "╔══════════════════════════════════════════════════════════════════╗\n" +
            "║  PRE-COMPUTED ROLE YEARS — AUTHORITATIVE, DO NOT RECALCULATE    ║\n" +
            "║  Computed from CV: COURSE=0, PRODUCTION=1.0x, FREELANCE=0.7x,  ║\n" +
            "║  INTERNSHIP=0.5x, PET_PROJECT=0.2x. Adjacent roles kept apart. ║\n" +
            "╚══════════════════════════════════════════════════════════════════╝\n" +
            $"  PM/PO weighted years          = {r.PmPo.ToString("F1", inv)}   (Product Manager, Product Owner, Head of Product)\n" +
            $"  PMM weighted years            = {r.Pmm.ToString("F1", inv)}   (Product Marketing Manager, Growth Manager)\n" +
            $"  Business Analyst weighted yrs = {r.BusinessAnalyst.ToString("F1", inv)}   (Business Analyst, Systems Analyst)\n" +
            $"  Project Manager weighted yrs  = {r.ProjectManager.ToString("F1", inv)}   (Project Manager, Program Manager)\n" +
            $"  Developer/Engineer weighted   = {r.Developer.ToString("F1", inv)}   (Software Engineer, Developer, etc.)\n" +
            $"  Data Analyst weighted yrs     = {r.DataAnalyst.ToString("F1", inv)}   (Data Analyst, Data Scientist)\n" +
            $"  Designer weighted yrs         = {r.Designer.ToString("F1", inv)}   (UX/UI Designer)\n" +
            $"  Marketing weighted yrs        = {r.Marketing.ToString("F1", inv)}   (Generic Marketing roles)\n" +
            "\n" +
            "Use these numbers in STEP 2 below. Do NOT calculate from the CV — these are final.";
    }

    private static string BuildOutputSpec() =>
        "Respond with ONLY valid JSON (no markdown, no text outside JSON):\n" +
        "{\n" +
        "  \"score\": <integer 0-100>,\n" +
        "  \"verdict\": \"<strong_fit|good_fit|partial_fit|weak_fit>\",\n" +
        "  \"matched\": \"<key skills/tools present in BOTH candidate AND job, comma-separated, max 6, or 'none'>\",\n" +
        "  \"gaps\": [\n" +
        "    {\"item\": \"<short name of missing requirement>\", \"severity\": \"<critical|moderate|minor>\"},\n" +
        "    ...\n" +
        "  ]   (empty array [] if no gaps; max 4 entries)\n" +
        "}\n" +
        "\n" +
        "GAPS FORMAT — STRICT:\n" +
        "  - Each gap MUST be an object with exactly two keys: \"item\" and \"severity\".\n" +
        "  - \"severity\" MUST be one of: critical, moderate, minor (lowercase, no other values).\n" +
        "  - The \"item\" text MUST NOT include the severity in parentheses — severity goes\n" +
        "    ONLY in the separate \"severity\" field. ❌ {\"item\":\"SQL (critical)\", ...} is WRONG.\n" +
        "    ✓ {\"item\":\"SQL\", \"severity\":\"critical\"} is CORRECT.\n" +
        "  - When 'eager to learn' / 'navchymo' applies to a skill → severity = minor (not critical).\n" +
        "  - When the job description forgives a missing requirement → severity = minor.\n" +
        "  - Empty array [] means no gaps. Do NOT use the string \"none\" — use [].";


    private const string HardCapsStep1 =
        "╔══════════════════════════════════════════════════════════════════╗\n" +
        "║  HARD CAPS — ABSOLUTE RULES, NO EXCEPTIONS                      ║\n" +
        "╚══════════════════════════════════════════════════════════════════╝\n" +
        "\n" +
        "STEP 1 — Identify what role and how many years the job requires.\n" +
        "  Look for: 'від X років', 'X+ years', 'мінімум X', 'at least X years', etc.\n" +
        "  If no experience required (entry-level) → required_years = 0.";

    private const string HardCapsStep2MapIntro =
        "STEP 2 — Look up the candidate's weighted years for THAT SPECIFIC ROLE\n" +
        "  from the PRE-COMPUTED ROLE YEARS above. Use the exact matching category provided\n" +
        "  by the family-specific mappings below.\n" +
        "  Adjacent roles DO NOT count — each bucket is independent.";

    private const string HardCapsStep3 =
        "STEP 3 — Apply the cap:\n" +
        "  • required_years = 1,  candidate_years = 0   → score MAX 62, verdict MAX partial_fit\n" +
        "  • required_years ≥ 2,  candidate_years = 0   → score MAX 30, verdict = weak_fit\n" +
        "  • required_years ≥ 2,  candidate_years < 1   → score MAX 52, verdict MAX partial_fit\n" +
        "  • required_years ≥ 3,  candidate_years ≤ 1   → score MAX 32, verdict = weak_fit\n" +
        "  • required_years ≥ 5,  candidate_years ≤ 2   → score MAX 22, verdict = weak_fit\n" +
        "  Always add: \"X+ years as [Role] (critical)\" to gaps.\n" +
        "\n" +
        "  ⚠️  No language in the job description overrides these caps:\n" +
        "    'ми цінуємо якість, а не роки' / 'компанія готова інвестувати' — caps still apply.";


    private const string MidSeniorJuniorCap =
        "STEP 4a — Seniority mismatch caps:\n" +
        "  • Job requires Middle/Senior, candidate is Junior → score MAX 55, verdict MAX partial_fit";

    private const string OverqualifiedCap =
        "STEP 4b — Overqualification cap:\n" +
        "  • Job is Junior/Mid, candidate is Senior (overqualified) → score MAX 67";

    private const string EngineeringMgrRule =
        "STEP 4c — Leadership-role cap:\n" +
        "  • Engineering Manager / Tech Lead → score ≤ 25, weak_fit for non-engineers.";

    private const string CoreFunctionMismatchPattern =
        "STEP 4d — Core function mismatch (universal pattern):\n" +
        "  If the job's PRIMARY daily work doesn't match the candidate's target_roles\n" +
        "  or relevant experience — even if title or shared keywords suggest similarity —\n" +
        "  treat as mismatch: score MAX 25, verdict = weak_fit, NO exceptions.\n" +
        "\n" +
        "  Pattern recognition (judge by DUTIES, not title or methodology vocabulary):\n" +
        "    - Same title but different primary duties → judge by duties\n" +
        "    - Shared methodology words (Agile, hypothesis, roadmap) but different work → mismatch\n" +
        "    - Title says X but actual responsibilities are Y → mismatch\n" +
        "\n" +
        "  Family-specific mismatch examples are appended below.";


    private const string JuniorFriendly =
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
        "  For career_switcher=true candidates, junior-friendly jobs are significantly more attainable.";

    private const string VerdictBands =
        "Verdict bands (score MUST fall inside the range):\n" +
        "  strong_fit  → 85-100: meets virtually all key requirements\n" +
        "  good_fit    → 65-84:  meets most requirements, gaps are learnable\n" +
        "  partial_fit → 35-64:  meets some requirements, notable gaps\n" +
        "  weak_fit    →  0-34:  fundamental mismatch\n" +
        "\n" +
        "CRITICAL constraint: if ANY gap is marked (critical) → verdict CANNOT be good_fit or strong_fit.\n" +
        "  A critical gap means a fundamental missing requirement → partial_fit MAX (score ≤ 64).\n" +
        "  Exception: if the only critical gap is '1yr as [role] (critical)' — this may still be good_fit\n" +
        "  for junior-friendly jobs where the company explicitly accepts candidates without experience.";

    private const string ScorePrecision =
        "Score precision:\n" +
        "- Use precise, unique values — NOT round multiples of 5.\n" +
        "  Good: 67, 71, 74, 76, 79, 83. Bad: 70, 75, 80.\n" +
        "- Every job must receive a DIFFERENT score.\n" +
        "- Score = REAL fit of THIS job vs THIS candidate.\n" +
        "- Within good_fit (65-84): 65-69 when critical gaps, 70-76 for 1-2 moderate gaps,\n" +
        "  77-84 for mostly minor gaps or strong alignment.\n" +
        "- Scores 85+ must be rare (top ~10%). Default toward strict when uncertain.";

    private const string ExperienceMultipliers =
        "Experience type multipliers (for assessing skills, seniority, NOT for role years):\n" +
        "  PRODUCTION=1.0x, FREELANCE=0.7x, INTERNSHIP=0.5x, PET_PROJECT=0.2x, COURSE=0.0x\n" +
        "  Junior=0-1yr weighted. Middle=2-4yrs. Senior=5+yrs.";


    private const string LanguageHandling =
        "English: if job requires B2+ and CV shows no English signals → add gap, reduce score 8-12.\n" +
        "Ukrainian employment gaps 2022-2023 are neutral (war context).\n" +
        "\n" +
        "Native language handling (Ukrainian/Russian):\n" +
        "- If the candidate profile has a `languages` field → use it directly.\n" +
        "  Example: languages=[{language:Ukrainian,level:native}] → Ukrainian is NOT a gap.\n" +
        "- If no `languages` field but the CV is from a Ukrainian candidate (Ukrainian education,\n" +
        "  Ukrainian location, or CV written in Ukrainian) → assume Ukrainian and Russian are native.\n" +
        "  NEVER add 'Ukrainian (critical/moderate)' or 'Russian (critical/moderate)' as a gap\n" +
        "  for a Ukrainian candidate. This is a systematic error — they are native speakers.";

    private const string CareerSwitcherGeneral =
        "Career switcher / junior without commercial experience (universal):\n" +
        "- career_switcher=true → DO NOT add 'lack of commercial experience in target role' as a gap.\n" +
        "  The experience cap already penalises this. Double-counting is forbidden — focus on\n" +
        "  SPECIFIC missing skills instead.\n" +
        "- DO NOT reduce score purely because the candidate has no commercial experience in target role.\n" +
        "  Instead, evaluate: does this candidate have the SPECIFIC skills this job requires?\n" +
        "- If yes AND the job is junior-friendly → score generously (55-68 range).\n" +
        "- 'unverified_skills' (soft skills: analytical thinking, adaptable, etc.) → NEVER in Matched.\n" +
        "  These are self-reported with no evidence. Only domain_skills and technical_skills count.";

    private const string EagerToLearn =
        "Eager to learn / will train signal:\n" +
        "  If the job description EXPLICITLY says it will accept someone without a specific skill\n" +
        "  IF they are willing to learn — that skill is NOT a critical gap. Treat it as minor.\n" +
        "  Trigger phrases (Ukrainian + English):\n" +
        "    'eager to learn', 'willing to learn', 'or equivalent training', 'готові навчати',\n" +
        "    'або готовність навчатись', 'або бажання розвиватись', 'open to candidates without',\n" +
        "    'technical background is a plus', 'we will teach', 'навчимо', 'розглянемо без досвіду в'\n" +
        "  Example: Enapps ERP says 'ERP experience or eager to learn + technical background' →\n" +
        "    ERP experience = minor gap (explicitly forgiven). Technical background the candidate HAS.\n" +
        "    This job should score HIGHER than a job requiring ERP experience without this language.";


    private const string PlatformToolsRule =
        "Platform-specific tool gaps — always critical if the job EXPLICITLY requires them:\n" +
        "  These tools require months of hands-on practice and cannot be faked in an interview.\n" +
        "  Only list as a gap if the job description SPECIFICALLY MENTIONS the tool as required.\n" +
        "  IMPORTANT: ONLY add these as gaps if the job EXPLICITLY names them. Do NOT add them\n" +
        "  speculatively. Generic skills do NOT compensate for absence of these specific tools.\n" +
        "  Family-specific platform-tool list appended below.";

    private const string DomainLock =
        "Non-transferable domain knowledge — treat as industry-locked:\n" +
        "  The following domains require deep prior industry experience that cannot be acquired\n" +
        "  quickly. Even junior roles in these domains expect some industry background.\n" +
        "  If job requires ANY of these and candidate has zero domain signals → score MAX 45:\n" +
        "  • Energy systems: EMS, BESS, VPP, smart grid, energy trading, SCADA, power management\n" +
        "  • Pharma/MedTech: regulatory affairs, clinical trials, CNS, oncology, drug lifecycle\n" +
        "  • Fintech/banking regulation: NBU, PSD2, SWIFT, AML compliance, core banking\n" +
        "  • Hardware/embedded: firmware, FPGA, embedded systems product management\n" +
        "  • Telecommunications: telco infrastructure, OSS/BSS, network management\n" +
        "  Family-specific industry tech signals (HL7/FHIR for pharma backend, PCI-DSS for banking, etc.)\n" +
        "  may be appended below when the active module provides them.";


    private const string ToolWeightMeta =
        "Tool weighting (universal meta-rule):\n" +
        "  Hard tools = months of hands-on practice required → absence when required = critical/moderate.\n" +
        "  Easy tools = days to learn → absence = minor only.\n" +
        "  Use your knowledge of the job's profession to classify tools mentioned in the description.\n" +
        "  Family-specific hard/easy tool lists are appended below.";

    private const string MatchedGapsRules =
        "Matched: only skills present in BOTH job description AND candidate CV.\n" +
        "  - Must be tied to domain_skills or technical_skills (real work/study context). Max 5.\n" +
        "  - NEVER include unverified_skills (soft skills without evidence) in Matched.\n" +
        "  - NEVER include skills the candidate only listed in a 'Skills' section without usage context.\n" +
        "Gaps severity (each gap = object {\"item\":\"...\", \"severity\":\"critical|moderate|minor\"}).\n" +
        "  - 'Preferred'/'plus'/'nice to have' in job → max minor gap.\n" +
        "  - 2+ years of ROLE experience for a candidate with 0 → critical.\n" +
        "  - 1 year of ROLE experience for a candidate with 0 → moderate.\n" +
        "    Rationale: 1yr role requirement is achievable with strong portfolio/skills for career-switcher.\n" +
        "  - 1+ year in a DOMAIN/INDUSTRY (fintech, gaming, iGaming, crypto, e-commerce, FMCG) → critical.\n" +
        "    Rationale: domain knowledge is non-transferable and hard to fake in an interview.\n" +
        "  - Missing hard tools when clearly required → critical (see Tool weight above).\n" +
        "  - Missing easy/soft tools → minor only.\n" +
        "Nothing matched → \"none\". No gaps → [] (empty array, not the string \"none\").";

    private const string Finale =
        "Score = REAL job fit, not keyword overlap. Be honest and precise.";
}
