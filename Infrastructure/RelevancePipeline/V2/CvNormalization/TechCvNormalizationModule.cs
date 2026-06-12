using Application.Common.CvNormalization;
using Application.Common.Enums;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.V2.CvNormalization;


public sealed class TechCvNormalizationModule : ICvNormalizationModule
{
    public CvDomain Domain => CvDomain.Tech;


    public string Version => "tech_v3";

    public CvNormalizationSlots GetSlots() => new(
        SeniorityBands:
            "weighted years across all experience. Multipliers: PRODUCTION 1.0, " +
            "FREELANCE 0.7, INTERNSHIP 0.5, PET_PROJECT 0.2, COURSE 0.0. Bands: " +
            "junior = 0–1 yr weighted, middle = 2–4 yrs, senior = 5+ yrs. Use " +
            "\"intern\" only when the CV explicitly says intern. \"not_specified\" " +
            "when no signal. Derive from weighted years and scope — not from job " +
            "titles alone.",

        EducationRelevanceGuide:
            "true if the program's typical curriculum prepares the candidate for " +
            "the roles in target_roles. For tech / product / data / engineering " +
            "targets these fields are relevant: Computer Science, Software " +
            "Engineering, IT, Data Science, Mathematics, Engineering (any " +
            "branch), Information Systems, applied tech disciplines. MBA is " +
            "relevant for product, management, and growth targets. Otherwise " +
            "judge by reasonable alignment.",

        TargetRolesGuidance:
            "SOURCE PRIORITY for what the candidate TARGETS (not what they did):\n" +
            "    (1) CV header tagline (the line under the name).\n" +
            "    (2) Professional summary's stated objective.\n" +
            "    (3) Title of the most recent training program / course.\n" +
            "  DO NOT include past job/project titles from experience entries — " +
            "those describe what the candidate DID, not what they target now. " +
            "A Full-Stack Developer role on a pet project is not a target if the " +
            "header says \"Junior Product Manager\".\n" +
            "  Always include domain qualifiers (\"Mobile\", \"Junior\", \"Senior\", " +
            "\"Backend\", \"Frontend\", \"Fullstack\", \"Embedded\", \"ML\", \"Data\", " +
            "\"DevOps\") when they appear in the header, summary, or training " +
            "title — they carry real targeting signal that downstream scoring uses.\n" +
            "  Worked example:\n" +
            "    Header:   \"Junior Product Manager\"\n" +
            "    Summary:  \"...impactful mobile applications\"\n" +
            "    Training: \"Mobile Product Manager Course\"\n" +
            "    Past project role (in experience): \"Full-Stack Developer\"\n" +
            "    → target_roles = [\"Junior Product Manager\",\n" +
            "                      \"Mobile Product Manager\"]\n" +
            "      NOT including \"Full-Stack Developer\" — past role, not a target.\n" +
            "      \"Mobile\" preserved because header + summary + training all " +
            "carry the mobile signal.",

        ExperienceTypeNotes:
            "PET_PROJECT applies broadly here — side projects, hackathon entries, " +
            "personal apps, open-source contributions without paying users.",

        CanonicalizationExamples:
            "Worked examples for software CVs:\n" +
            "    \"SQL for data analysis\" + \"SQL databases\" + \"SQL\"\n" +
            "        → \"SQL\"\n" +
            "    \"Agile\" + \"Scrum\" + \"Agile/Scrum methodology\"\n" +
            "        → \"Agile/Scrum\"\n" +
            "    \"Hypothesis formulation\" + \"Hypothesis validation\"\n" +
            "        → \"Hypothesis validation\"\n" +
            "    \"Mobile monetization strategies\" + \"Mobile monetization\"\n" +
            "        → \"Mobile monetization\"\n" +
            "    \"REST API understanding\" + \"REST API\"\n" +
            "        → \"REST API\"\n" +
            "    Parenthesised stacks — SPLIT into sibling skills, do NOT collapse:\n" +
            "      \"C# (.NET Core, ASP.NET, EF Core)\"\n" +
            "        → \"C#\", \"ASP.NET Core\", \"EF Core\"  (three separate entries)\n" +
            "      \"JavaScript (React, Vue, Angular)\"\n" +
            "        → \"JavaScript\", \"React\", \"Vue\", \"Angular\"\n" +
            "      Rationale: the parent and each parenthesised item are distinct\n" +
            "      skills, not variants of one concept.",

        FullWorkedExample: YanWorkedExample);


    private const string YanWorkedExample =
        "Sample CV input:\n" +
        "\n" +
        "    YAN KHADNEVICH | Junior Product Manager | Kyiv, Ukraine\n" +
        "\n" +
        "    PROFESSIONAL SUMMARY\n" +
        "    Junior Product Manager with technical background in software\n" +
        "    development (.NET, React) and formal training in mobile product\n" +
        "    management. Strong foundation in market research, hypothesis\n" +
        "    validation, unit economics, and data-driven decision making.\n" +
        "    Seeking to leverage technical expertise and product thinking in\n" +
        "    building impactful mobile applications.\n" +
        "\n" +
        "    PRODUCT MANAGEMENT EXPERIENCE\n" +
        "    AI Photo Enhancement App (Training Project) — Product Manager\n" +
        "      Mobile Product Manager Training Course\n" +
        "      • Conducted market research and competitive analysis for AI\n" +
        "        photo editing market\n" +
        "      • Formulated and prioritized 5 hypotheses using ICE framework\n" +
        "      • Designed MVP scope with feature prioritization and 8-week\n" +
        "        development roadmap\n" +
        "      • Built financial model and calculated unit economics\n" +
        "      • Defined analytics framework using Amplitude for event\n" +
        "        tracking and retention monitoring\n" +
        "\n" +
        "    FPV Drone Flight Controller Testing Platform — Product Manager\n" +
        "      Genesis MVP Camp | 4-day intensive program\n" +
        "      • Led product discovery for web platform automating FPV drone\n" +
        "        flight controller testing and validation\n" +
        "      • Conducted market research to validate product demand\n" +
        "      • Developed Business Model Canvas and learned MVP methodology\n" +
        "        including customer discovery and rapid iteration\n" +
        "\n" +
        "    TECHNICAL EXPERIENCE\n" +
        "    .NET Development Training — Student\n" +
        "      EPAM University Program\n" +
        "      • Completed advanced C# courses covering design patterns,\n" +
        "        application architecture, and ASP.NET Core\n" +
        "      • Practiced Agile/Scrum methodology through hands-on projects\n" +
        "\n" +
        "    AcademicHub Platform — Full-Stack Developer\n" +
        "      Team Project (4 members)\n" +
        "      • Developed open-source learning platform in 4-person team\n" +
        "        using C# backend and React frontend\n" +
        "      • Conducted market research and utilized GitHub Actions for\n" +
        "        CI/CD workflows\n" +
        "\n" +
        "    EDUCATION\n" +
        "    Kyiv School of Economics\n" +
        "    Bachelor of Science in Software Engineering and Business\n" +
        "    Analysis | 2021 – Present (4th year)\n" +
        "\n" +
        "    PROFESSIONAL TRAINING\n" +
        "    • Mobile Product Manager Course — Market research, hypothesis\n" +
        "      validation, MVP development, unit economics, mobile monetization\n" +
        "    • Genesis MVP Camp — MVP methodology, customer discovery,\n" +
        "      Business Model Canvas\n" +
        "    • EPAM .NET Training — Advanced C#, design patterns, ASP.NET Core\n" +
        "\n" +
        "    SKILLS\n" +
        "    Product Management: Market research & competitive analysis,\n" +
        "      Hypothesis formulation & validation, Feature prioritization\n" +
        "      (ICE framework), MVP scope definition & roadmapping, Unit\n" +
        "      economics (LTV/CAC, retention), Mobile monetization strategies\n" +
        "    Analytics & Data: Amplitude (event tracking, funnels), Financial\n" +
        "      modeling, SQL for data analysis, Excel/Google Sheets (advanced)\n" +
        "    Technical Skills: C# (.NET Core, ASP.NET, EF Core), JavaScript,\n" +
        "      TypeScript, React, SQL & NoSQL databases, REST API\n" +
        "      understanding, Git, GitHub Actions, Docker, AWS basics\n" +
        "    Soft Skills: Analytical thinking, Data-driven decision making,\n" +
        "      Technical communication, Cross-functional collaboration,\n" +
        "      Quick learner & adaptable\n" +
        "\n" +
        "    Languages: English (B2), Ukrainian (Native)\n" +
        "\n" +
        "Ideal extraction:\n" +
        "\n" +
        "    {\n" +
        "      \"seniority\": \"junior\",\n" +
        "      \"target_roles\": [\"Junior Product Manager\", \"Mobile Product Manager\"],\n" +
        "      \"domain_skills\": [\n" +
        "        \"Market research\", \"Competitive analysis\", \"Hypothesis validation\",\n" +
        "        \"ICE framework\", \"MVP scope definition\", \"Roadmapping\",\n" +
        "        \"Unit economics\", \"Amplitude\", \"Financial modeling\",\n" +
        "        \"Customer discovery\", \"Business Model Canvas\", \"MVP methodology\",\n" +
        "        \"Rapid iteration\", \"C#\", \"ASP.NET Core\", \"Design patterns\",\n" +
        "        \"Application architecture\", \"React\", \"GitHub Actions\",\n" +
        "        \"Agile/Scrum\", \"Mobile monetization\"\n" +
        "      ],\n" +
        "      \"technical_skills\": [\n" +
        "        \".NET Core\", \"EF Core\", \"JavaScript\", \"TypeScript\", \"SQL\",\n" +
        "        \"NoSQL databases\", \"REST API\", \"Git\", \"Docker\", \"AWS basics\",\n" +
        "        \"Excel\", \"Google Sheets\"\n" +
        "      ],\n" +
        "      \"unverified_skills\": [\n" +
        "        \"Analytical thinking\", \"Data-driven decision making\",\n" +
        "        \"Technical communication\", \"Cross-functional collaboration\",\n" +
        "        \"Quick learner\", \"Adaptable\"\n" +
        "      ],\n" +
        "      \"experience\": [\n" +
        "        {\"title\": \"AI Photo Enhancement App (Training Project)\", \"type\": \"COURSE\", \"duration_months\": 3, \"years_ago\": 0},\n" +
        "        {\"title\": \"FPV Drone Flight Controller Testing Platform\", \"type\": \"COURSE\", \"duration_months\": 1, \"years_ago\": 0},\n" +
        "        {\"title\": \".NET Development Training\", \"type\": \"COURSE\", \"duration_months\": 3, \"years_ago\": 0},\n" +
        "        {\"title\": \"AcademicHub Platform\", \"type\": \"PET_PROJECT\", \"duration_months\": 6, \"years_ago\": 0}\n" +
        "      ],\n" +
        "      \"education\": {\n" +
        "        \"degree\": \"bachelor\",\n" +
        "        \"field\": \"Software Engineering and Business Analysis\",\n" +
        "        \"is_relevant\": true,\n" +
        "        \"status\": \"in_progress\",\n" +
        "        \"current_year\": 4,\n" +
        "        \"graduation_year\": 2025\n" +
        "      },\n" +
        "      \"english_level\": \"B2\",\n" +
        "      \"languages\": [\n" +
        "        {\"language\": \"Ukrainian\", \"level\": \"native\"},\n" +
        "        {\"language\": \"English\", \"level\": \"B2\"}\n" +
        "      ],\n" +
        "      \"has_real_product_experience\": false,\n" +
        "      \"career_switcher\": true\n" +
        "    }\n" +
        "\n" +
        "Key decisions demonstrated in this example:\n" +
        "  - \"Excel\", \"Google Sheets\", \".NET Core\", \"EF Core\" → technical_skills:\n" +
        "    they appear ONLY in the Skills section (or only inside the\n" +
        "    parenthesised \"C# (.NET Core, ASP.NET, EF Core)\" stack), with no\n" +
        "    experience-bullet evidence.\n" +
        "  - \"ASP.NET Core\" → domain_skills: also mentioned in the .NET\n" +
        "    Development Training bullet (\"...design patterns, application\n" +
        "    architecture, and ASP.NET Core\").\n" +
        "  - \"Customer discovery\", \"Rapid iteration\", \"Design patterns\",\n" +
        "    \"Application architecture\", \"MVP methodology\" → domain_skills:\n" +
        "    all appear in experience bullets, even though they sound like\n" +
        "    soft methodology terms — they are concrete named methods.\n" +
        "  - \"Mobile monetization\" → domain_skills: listed under the Mobile\n" +
        "    Product Manager Course training (an experience entry of type\n" +
        "    COURSE) in the Professional Training section.\n" +
        "  - target_roles = [Junior PM, Mobile PM]: header + summary +\n" +
        "    training all carry the Mobile signal. \"Full-Stack Developer\"\n" +
        "    is NOT a target — it's a past project role.\n" +
        "  - duration_months for FPV Drone = 1: the CV says \"4-day intensive\n" +
        "    program\", which is closer to 1 month than to the default 3.\n" +
        "  - graduation_year = 2025: \"(4th year)\" + bachelor (4-year program)\n" +
        "    starting in 2021 → 2025.";
}
