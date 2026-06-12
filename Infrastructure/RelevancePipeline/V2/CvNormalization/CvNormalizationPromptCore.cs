using Application.Common.CvNormalization;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public static class CvNormalizationPromptCore
{


    public const string Version = "v5_1_confidence";

    public static string Build(string cvRawText, CvNormalizationSlots slots) =>
        "You are a CV parsing expert. Extract a structured candidate profile from the CV below.\n" +
        "Follow the procedure exactly. The output JSON schema is enforced by the runtime —\n" +
        "your job is to fill it with correct values, not to format the JSON yourself.\n\n" +


        "TODAY'S DATE: " + DateTime.UtcNow.ToString("yyyy-MM-dd") +
        " — use this exact date whenever the CV says \"Present\", " +
        "\"Current\", or leaves an end-date open.\n\n" +

        "CV text:\n" +
        cvRawText + "\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "A. EXPERIENCE\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "An \"experience entry\" is any block describing a project, job, course, or\n" +
        "training. Header format is typically \"<Project/Company Name> — <Role>\" or\n" +
        "\"<Role> at <Company>\". Bullets underneath describe what was done.\n\n" +

        "For each entry, fill these fields:\n\n" +

        "  title — the PROJECT or COMPANY name, written verbatim from the CV.\n" +
        "    This is the noun phrase BEFORE the em-dash (—) in the header line.\n" +
        "    NEVER the role label after the em-dash. NEVER a generic word like\n" +
        "    \"Student\", \"Developer\", or \"Product Manager\".\n" +
        "      Header \"AcademicHub Platform — Full-Stack Developer\"\n" +
        "        → title = \"AcademicHub Platform\"   (not \"Full-Stack Developer\")\n" +
        "      Header \".NET Development Training — Student\"\n" +
        "        → title = \".NET Development Training\"   (not \"Student\")\n" +
        "      Header \"AI Photo Enhancement App (Training Project) — Product Manager\"\n" +
        "        → title = \"AI Photo Enhancement App (Training Project)\"\n" +
        "    If the entry truly has only a role + company (no project name),\n" +
        "    use \"Role (Company)\" — e.g. \"Software Engineer (Google)\".\n\n" +

        "  type — one of:\n" +
        "    PRODUCTION   real company, paying users/clients, full or contracted role\n" +
        "    FREELANCE    real paid client work, deliverable shipped\n" +
        "    INTERNSHIP   real company, structured intern program, limited scope\n" +
        "    PET_PROJECT  personal or team project without paying users\n" +
        "    COURSE       any training program, bootcamp, university course, or\n" +
        "                 workshop — INCLUDING projects done as part of a training\n" +
        "                 program (e.g. \"Mobile PM Course — Final Project: PhotoApp\"\n" +
        "                 is COURSE, not PET_PROJECT).\n" +
        AppendSlot(slots.ExperienceTypeNotes) +

        "  duration_months — integer, NEVER null. Use explicit dates if given;\n" +
        "    otherwise estimate by type:\n" +
        "      COURSE / training / bootcamp                → 3\n" +
        "      Student in degree program (in_progress)     → current_year × 8\n" +
        "      INTERNSHIP without dates                    → 4\n" +
        "      PET_PROJECT without dates                   → 6\n" +
        "      FREELANCE without dates                     → 6\n" +
        "      PRODUCTION without dates                    → 12\n\n" +

        "  years_ago — integer, NEVER null. Years since the entry ENDED.\n" +
        "    Current/ongoing or ended this year → 0. Estimate from context when\n" +
        "    explicit dates are missing.\n\n" +

        "═══════════════════════════════════════════════════════════════\n" +
        "B. SKILLS\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "B1. EXTRACT — read the entire CV. List every NAMED COMPETENCY the\n" +
        "    candidate can credibly claim and that an employer would recognise\n" +
        "    as a hireable skill: a tool, framework, language, library, named\n" +
        "    methodology, certification, defined technique, or specific recurring\n" +
        "    practice with a recognised name.\n\n" +

        "    DO NOT EXTRACT — these are NOT skills, even though they appear in\n" +
        "    the CV text:\n" +
        "      - Activity verbs and process names:\n" +
        "          \"software development\", \"building applications\",\n" +
        "          \"data analysis\", \"market research\" (as an activity verb).\n" +
        "        These describe what was DONE, not the skill used.\n" +
        "      - Markets, products, or domain nouns from bullets:\n" +
        "          \"AI photo editing market\", \"FPV drone testing market\",\n" +
        "          \"open-source learning platform\".\n" +
        "        Context, not skills.\n" +
        "      - Outcomes, deliverables, or artefacts:\n" +
        "          \"positive ROI\", \"8-week development roadmap\",\n" +
        "          \"analytics framework\" (when used as \"defined analytics\n" +
        "          framework\" — the deliverable).\n" +
        "        What was delivered, not a competency.\n" +
        "      - Multi-clause project descriptions:\n" +
        "          \"web platform automating FPV drone flight controller testing\n" +
        "          and validation\".\n" +
        "        These belong in experience.title, never in skills.\n\n" +

        "    Examples of what TO extract from CV-style text:\n" +
        "      \"C#\", \"React\", \"Amplitude\", \"ICE framework\", \"Agile/Scrum\",\n" +
        "      \"Hypothesis validation\", \"Unit economics\", \"Customer discovery\",\n" +
        "      \"Mobile monetization\", \"REST API\", \"GitHub Actions\",\n" +
        "      \"Business Model Canvas\", \"ASP.NET Core\", \"Financial modeling\".\n\n" +

        "B2. CANONICALIZE — the SAME underlying skill must appear in exactly ONE\n" +
        "    canonical form. Merge variants before classifying. Do NOT collapse\n" +
        "    parenthesised stacks of distinct frameworks into a single skill —\n" +
        "    those are sibling skills, each gets its own entry.\n" +
        AppendSlot(slots.CanonicalizationExamples) +

        "B3. CLASSIFY — for each canonical skill, ask these questions IN ORDER.\n" +
        "    The FIRST Yes answer determines which list it goes in.\n" +
        "    The three lists (domain_skills, technical_skills, unverified_skills)\n" +
        "    are MUTUALLY EXCLUSIVE — every skill appears in exactly one.\n\n" +

        "    Q1. Is this item a JOB ROLE or TITLE — e.g. \"Mobile Product Manager\",\n" +
        "        \"Backend Developer\", \"Product Owner\", \"Data Engineer\",\n" +
        "        \"Registered Nurse\", \"Associate Attorney\"?\n" +
        "        → Yes: it is NOT a skill. Drop from all three skill lists.\n" +
        "                (It may belong in target_roles if it describes what the\n" +
        "                candidate is targeting.)\n\n" +

        "    Q2. Is this item a TRAIT of the candidate — a personal quality,\n" +
        "        mindset, attitude, or work habit (NOT an action they did,\n" +
        "        NOT a thing they built, NOT a market or product, NOT an\n" +
        "        outcome they delivered)?\n" +
        "        Trait examples (these belong in unverified_skills):\n" +
        "          \"Analytical thinking\", \"Cross-functional collaboration\",\n" +
        "          \"Data-driven decision making\", \"Quick learner\", \"Adaptability\",\n" +
        "          \"Strategic thinking\", \"Customer focus\", \"Technical communication\",\n" +
        "          \"Attention to detail\", \"Bedside manner\".\n" +
        "        → Yes: unverified_skills.\n\n" +

        "    Q3. Is this item a CONCRETE tool / framework / language / platform /\n" +
        "        named methodology / certified procedure, AND does the CV mention\n" +
        "        it inside a BULLET POINT of any experience entry (= the candidate\n" +
        "        actually USED it)?\n" +
        "        → Yes: domain_skills.\n" +
        "          (Example: AcademicHub bullets say \"C# backend and React frontend\"\n" +
        "           → both C# and React are domain_skills, evidence-backed.)\n\n" +

        "    Q4. Otherwise — concrete skill that appears only in a standalone Skills\n" +
        "        section or summary, with no experience-bullet evidence.\n" +
        "        → technical_skills.\n" +
        "          (Example: Skills list shows \"Docker, AWS basics\" but no experience\n" +
        "           entry mentions them → both technical_skills.)\n\n" +

        "    WORKED EXAMPLE — applying Q3 vs Q4 to a CV with BOTH experience\n" +
        "    bullets AND a Skills section. Read carefully — this is the most\n" +
        "    common misclassification.\n\n" +
        "      Suppose the CV contains:\n" +
        "        Experience bullets (under some project entry):\n" +
        "          \"Developed open-source learning platform using C# backend\n" +
        "           and React frontend\"\n" +
        "          \"Utilized GitHub Actions for CI/CD workflows\"\n" +
        "        Skills section, separate part of the CV:\n" +
        "          Technical Skills\n" +
        "            - C# (.NET Core, ASP.NET, EF Core)\n" +
        "            - JavaScript, TypeScript, React\n" +
        "            - SQL & NoSQL databases\n" +
        "            - REST API understanding\n" +
        "            - Git, GitHub Actions\n" +
        "            - Docker, AWS basics\n\n" +
        "      Resulting classification:\n" +
        "        domain_skills    = [\"C#\", \"React\", \"GitHub Actions\",\n" +
        "                            \"ASP.NET Core\"]\n" +
        "            (only items the CV places inside experience-entry bullets;\n" +
        "             ASP.NET Core qualifies if mentioned in any training/.NET\n" +
        "             bullet — otherwise it goes to technical_skills.)\n" +
        "        technical_skills = [\".NET Core\", \"EF Core\", \"JavaScript\",\n" +
        "                            \"TypeScript\", \"SQL\", \"NoSQL databases\",\n" +
        "                            \"REST API\", \"Git\", \"Docker\", \"AWS basics\"]\n" +
        "            (items present ONLY in the Skills section, with no\n" +
        "             experience-bullet mention).\n\n" +
        "      KEY DISTINCTION:\n" +
        "        - A skill that appears in BOTH a bullet AND the Skills section\n" +
        "          goes to domain_skills (experience evidence wins).\n" +
        "        - A skill that appears ONLY in the Skills section, no matter\n" +
        "          how prominently listed, goes to technical_skills.\n" +
        "        - technical_skills is NEVER empty for a tech CV that has a\n" +
        "          Skills section: there will always be items not covered by\n" +
        "          experience bullets.\n\n" +

        AppendSlot(slots.SkillBucketingNotes) +

        "═══════════════════════════════════════════════════════════════\n" +
        "C. OTHER FIELDS\n" +
        "═══════════════════════════════════════════════════════════════\n\n" +

        "seniority — " + slots.SeniorityBands + "\n\n" +

        "target_roles — what the candidate is targeting, ordered by emphasis:\n" +
        "  CV header > professional summary > most-recent role/training. Max 3.\n" +
        "  Use the candidate's own wording.\n" +
        "  " + slots.TargetRolesGuidance + "\n\n" +

        "education\n" +
        "  degree          — one of: bachelor | master | phd | associate | none\n" +
        "  field           — verbatim program name from the CV; empty string if absent.\n" +
        "  is_relevant     — " + slots.EducationRelevanceGuide + "\n" +
        "  status          — \"in_progress\" if the CV says current/present;\n" +
        "                    \"completed\" otherwise.\n" +
        "  current_year    — integer 1–6 when in_progress; null when completed.\n" +
        "  graduation_year — integer. If a year is not stated explicitly but\n" +
        "                    current_year is known AND degree length is implied,\n" +
        "                    infer it: start_year + degree_length (bachelor 4,\n" +
        "                    master 2, associate 2, phd 4-5). Example: CV says\n" +
        "                    \"2021 – Present (4th year)\" + bachelor →\n" +
        "                    graduation_year = 2025. Use null ONLY when neither\n" +
        "                    explicit year nor inference signal is available.\n\n" +

        "english_level — explicit signals first (\"B2\", \"Upper-Intermediate\" → B2,\n" +
        "  \"Fluent\" → C1, \"Native\" → native). If none, infer from international\n" +
        "  experience. \"not_specified\" when no signal exists.\n\n" +

        "languages — list ALL spoken languages, ordered native-first then descending.\n" +
        "  - Always include English at english_level (skip when english_level is\n" +
        "    not_specified).\n" +
        "  - If CV is written in Ukrainian, or mentions Ukrainian education or\n" +
        "    location without an explicit language line, add Ukrainian: native.\n" +
        "  - Add Russian ONLY when the CV explicitly mentions Russian proficiency\n" +
        "    (e.g. \"Russian: native\", \"Russian-speaking clients\"). Do NOT\n" +
        "    auto-assume Russian for Ukrainian candidates — many do not consider\n" +
        "    it native.\n" +
        "  - Add any other language explicitly mentioned in the CV.\n\n" +

        "has_real_product_experience — true if and only if at least one experience\n" +
        "  entry has type = PRODUCTION or FREELANCE. COURSE / INTERNSHIP / PET_PROJECT\n" +
        "  alone → false.\n\n" +

        "career_switcher — true when the candidate's primary domain is clearly\n" +
        "  changing (e.g. developer → product, designer → data, engineer → PM,\n" +
        "  nurse → healthcare admin, lawyer → compliance). Look at the combination\n" +
        "  of degree, recent training, and target_roles — a software-engineering\n" +
        "  student with PM training targeting Junior Product Manager IS a switcher.\n\n" +

        "confidence — self-reported certainty in [0.0, 1.0] about THIS extraction:\n" +
        "  1.0 → CV is detailed (>1500 chars), has explicit Experience / Education /\n" +
        "        Skills sections, role titles are concrete, years/dates given.\n" +
        "  0.8 → minor ambiguity: dates partially missing, one skill canonicalised by\n" +
        "        guess, mild seniority cue from job duties rather than explicit title.\n" +
        "  0.6 → CV is moderately short (500-1500 chars) OR sections are intermixed\n" +
        "        without clear headers. Best-guess extraction; downstream should treat\n" +
        "        the result as soft.\n" +
        "  0.4 → substantial missing information: <500 chars, no concrete dates, role\n" +
        "        titles absent or generic (\"specialist\", \"freelancer\"). Flag for human\n" +
        "        review.\n" +
        "  0.2 → near-empty input (1-3 sentences, no concrete signal). Output is mostly\n" +
        "        empty arrays / \"not_specified\".\n" +
        "Lowering confidence does NOT change the extracted fields — it only flags the\n" +
        "result as uncertain for downstream (matcher, UI). Always emit confidence.\n\n" +

        AppendWorkedExample(slots.FullWorkedExample);


    private static string AppendWorkedExample(string example) =>
        string.IsNullOrWhiteSpace(example)
            ? string.Empty
            : "\n\n" +
              "═══════════════════════════════════════════════════════════════\n" +
              "FULL WORKED EXAMPLE — apply the same extraction to the CV at the\n" +
              "top of this prompt. Treat the block below as REFERENCE ONLY; do\n" +
              "NOT extract from the sample CV, use it only as a calibration aid.\n" +
              "═══════════════════════════════════════════════════════════════\n\n" +
              example;


    private static string AppendSlot(string slotValue) =>
        string.IsNullOrWhiteSpace(slotValue) ? "\n" : "  " + slotValue + "\n\n";
}
