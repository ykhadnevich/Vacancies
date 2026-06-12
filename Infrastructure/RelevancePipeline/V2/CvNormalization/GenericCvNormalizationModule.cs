using Application.Common.CvNormalization;
using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public sealed class GenericCvNormalizationModule : ICvNormalizationModule
{
    public CvDomain Domain => CvDomain.Generic;


    public string Version => "generic_v2";

    public CvNormalizationSlots GetSlots() => new(
        SeniorityBands:
            "weighted years across experience, adjusted for industry norms. " +
            "Multipliers: PRODUCTION 1.0, FREELANCE 0.7, INTERNSHIP 0.5, " +
            "PET_PROJECT 0.2, COURSE 0.0. The software-default bands (junior 0–1 / " +
            "middle 2–4 / senior 5+) are too aggressive for many industries — " +
            "adjust using the CV's industry context:\n" +
            "    Healthcare / nursing / pharma: junior 0–2, middle 3–6, senior 7+.\n" +
            "    Legal / law firm: junior 0–3, middle 4–7, senior 8+.\n" +
            "    Education / academia: junior 0–2, middle 3–6, senior 7+.\n" +
            "    Skilled trades / construction: junior 0–2, middle 3–7, senior 8+.\n" +
            "    Sales / BD / marketing: junior 0–1, middle 2–4, senior 5+.\n" +
            "    Finance / accounting / banking: junior 0–2, middle 3–5, senior 6+.\n" +
            "    Creative / design / writing: junior 0–2, middle 3–5, senior 6+.\n" +
            "  Use \"intern\" only when the CV explicitly says intern. " +
            "\"not_specified\" when no signal exists.",

        EducationRelevanceGuide:
            "true if the program's typical curriculum prepares the candidate for " +
            "the roles in target_roles. Judge by alignment between degree field " +
            "and target domain:\n" +
            "    Medicine / Nursing / Pharmacy / Biology → healthcare roles.\n" +
            "    Law / Paralegal Studies / Jurisprudence → legal roles.\n" +
            "    Education / Pedagogy / Subject Mastery  → teaching roles.\n" +
            "    Business / Management / MBA             → management, sales, BD, ops.\n" +
            "    Accounting / Finance / Economics        → finance, audit, banking.\n" +
            "    Arts / Design / Communications          → creative, design, writing.\n" +
            "    Engineering / CS / Math / IT            → tech and engineering.\n" +
            "  When the degree field shares core vocabulary with the target role,\n" +
            "  default to true. When uncertain, weigh the candidate's overall CV\n" +
            "  signal: experience entries that align with the target_roles are " +
            "stronger evidence than the degree label alone.",

        TargetRolesGuidance:
            "Use the candidate's own industry-specific wording, including " +
            "specialty qualifiers (e.g. \"Registered Nurse — ICU\", \"Associate " +
            "Attorney — IP Litigation\", \"Senior Designer — Brand\", \"Account " +
            "Executive — SaaS\", \"Senior Financial Analyst — M&A\"). Qualifiers " +
            "are real targeting signal and downstream scoring uses them.",

        ExperienceTypeNotes:
            "PET_PROJECT applies mainly in software and creative contexts. For " +
            "healthcare, legal, finance, education, sales, and skilled-trades CVs " +
            "it is rarely applicable — most entries map to PRODUCTION, FREELANCE, " +
            "INTERNSHIP, or COURSE. Default to INTERNSHIP for structured early-" +
            "career roles (residency, articling, fellowship), FREELANCE for " +
            "independent paid work, and PRODUCTION for staff positions. Use " +
            "PET_PROJECT only when the CV explicitly describes a personal/portfolio " +
            "project without paying users.",

        CanonicalizationExamples:
            "General canonicalization examples:\n" +
            "    Variant phrasings of the same competency → single canonical form\n" +
            "      (e.g. \"Drafting contracts\" + \"Contract drafting\" → \"Contract drafting\").\n" +
            "    Methodology + tool stated together → keep them together\n" +
            "      (e.g. \"Agile/Scrum\" → \"Agile/Scrum\", not split).\n" +
            "    Credential / certification stacks — SPLIT into siblings:\n" +
            "      \"RN (BSN, CCRN)\"     → \"RN\", \"BSN\", \"CCRN\"\n" +
            "      \"CPA (Big 4 audit)\"  → \"CPA\" plus the experience context as a domain skill\n" +
            "      Rationale: each credential is an independent competency, not a\n" +
            "      variant of the parent.\n" +
            "    Software-style parenthesised stacks (when present in mixed CVs):\n" +
            "      \"C# (.NET Core, ASP.NET, EF Core)\"\n" +
            "        → \"C#\", \"ASP.NET Core\", \"EF Core\"  (three separate entries).",


        FullWorkedExample: string.Empty,

        SkillBucketingNotes:
            "OVERRIDE for NON-SOFTWARE CVs (healthcare, legal, education, " +
            "finance, sales, creative, academia, trades, etc.) — applies " +
            "instead of the Q3/Q4 worked example above:\n\n" +
            "  For non-software CVs there is no meaningful distinction between\n" +
            "  \"primary skill\" and \"secondary skill\" — the candidate's\n" +
            "  methodology, named techniques, certifications, regulated\n" +
            "  procedures, and the daily tools of their trade are ALL their\n" +
            "  core competencies and ALL belong in domain_skills.\n\n" +
            "  Rules:\n" +
            "    - domain_skills      → every concrete role-relevant skill,\n" +
            "                           whether it appears in an experience\n" +
            "                           bullet or only in the Skills section.\n" +
            "                           This includes the named tools of the\n" +
            "                           trade (Google Classroom, Quizlet,\n" +
            "                           1C 8.3, Salesforce, AppsFlyer,\n" +
            "                           Greenhouse, etc.).\n" +
            "    - technical_skills   → typically EMPTY for non-software CVs.\n" +
            "                           Populate only when the CV explicitly\n" +
            "                           mentions a skill that is clearly\n" +
            "                           outside the candidate's main domain\n" +
            "                           (e.g. a teacher who notes hobbyist\n" +
            "                           Python from a Coursera course).\n" +
            "    - unverified_skills  → unchanged — soft traits only, as\n" +
            "                           defined in Q2.\n\n" +
            "  Worked example — English Language Teacher CV:\n" +
            "    Skills section reads:\n" +
            "      Methodology: CLIL, task-based learning, communicative\n" +
            "         approach, formative assessment.\n" +
            "      Tools: Google Classroom, Zoom, Quizlet, Kahoot.\n" +
            "    Resulting classification:\n" +
            "      domain_skills    = [\"CLIL\", \"Task-based learning\",\n" +
            "                          \"Communicative approach\",\n" +
            "                          \"Formative assessment\",\n" +
            "                          \"Google Classroom\", \"Zoom\",\n" +
            "                          \"Quizlet\", \"Kahoot\"]\n" +
            "      technical_skills = []   (no out-of-domain skill mentioned)\n\n" +
            "  RECAP: software CVs follow Q3/Q4. Non-software CVs collapse\n" +
            "  Q3 and Q4 into domain_skills.\n\n");
}
