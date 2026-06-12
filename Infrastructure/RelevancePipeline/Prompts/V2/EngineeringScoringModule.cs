using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class EngineeringScoringModule : IScoringModule
{
    public RoleFamily Family => RoleFamily.Engineering;
    public string Version => "eng_v1";

    private readonly EngFamilyCaps _caps = new();

    public IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx) => _slots;

    public IReadOnlyList<RoleBucketMapping> GetBucketMappings() => _bucketMappings;

    public IReadOnlyList<AdjacencyRule> GetAdjacencyRules() => _adjacencyRules;

    public IReadOnlyList<MismatchExample> GetMismatchList() => _mismatchExamples;

    public IReadOnlyList<CareerPattern> GetCareerPatterns() => _careerPatterns;

    public IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx) => _toolWeights;

    public IFamilyCaps GetCapsLogic() => _caps;


    private static readonly IReadOnlyList<RoleBucketMapping> _bucketMappings = new[]
    {
        new RoleBucketMapping("Backend Developer/Engineer (Java, Go, C#, Python server-side)",
            RoleBucketId.Backend),
        new RoleBucketMapping("Frontend Developer (React, Vue, Angular, UI)",
            RoleBucketId.Frontend),
        new RoleBucketMapping("Fullstack Developer",
            RoleBucketId.Fullstack),
        new RoleBucketMapping("Mobile Developer (iOS / Android / RN / Flutter)",
            RoleBucketId.Mobile),
        new RoleBucketMapping("DevOps / SRE / Platform Engineer",
            RoleBucketId.DevOps),
        new RoleBucketMapping("QA Automation / SDET / Test Engineer",
            RoleBucketId.Qa),
        new RoleBucketMapping("ML Engineer / MLOps",
            RoleBucketId.MlEngineer),
        new RoleBucketMapping("Data Engineer",
            RoleBucketId.DataEngineer),
        new RoleBucketMapping("Embedded / Firmware Engineer",
            RoleBucketId.Embedded),
    };


    private static readonly IReadOnlyList<AdjacencyRule> _adjacencyRules = new[]
    {
        new AdjacencyRule(".NET", "Java", 4, 7, AdjacencyDirection.Symmetric,
            Note: "Both OOP+managed runtime; ecosystems similar enough to retrain quickly."),
        new AdjacencyRule(".NET", "Python", 10, 15, AdjacencyDirection.Symmetric,
            Note: "Significant retraining: static vs dynamic typing, different ecosystems."),
        new AdjacencyRule(".NET", "Go", 12, 18, AdjacencyDirection.Symmetric,
            Note: "Different paradigms; Go's runtime/concurrency requires re-learning."),
        new AdjacencyRule("React", "Vue", 2, 4, AdjacencyDirection.Symmetric),
        new AdjacencyRule("React", "Angular", 4, 7, AdjacencyDirection.Symmetric,
            Note: "Angular adds DI + TypeScript-heavy patterns."),
        new AdjacencyRule("iOS", "Android", 8, 12, AdjacencyDirection.Symmetric,
            Note: "Different language + tooling + UI framework."),
        new AdjacencyRule("PostgreSQL", "MongoDB", 8, 12, AdjacencyDirection.Symmetric,
            Note: "Relational vs document model — schema design philosophy differs."),
        new AdjacencyRule("PostgreSQL", "MySQL", 1, 2, AdjacencyDirection.Symmetric),
    };


    private static readonly IReadOnlyList<MismatchExample> _mismatchExamples = new[]
    {
        new MismatchExample("Sales Engineer",
            "technical sales / pre-sales / customer demos — NOT IC engineering"),
        new MismatchExample("Solutions Architect (pre-sales)",
            "architecture consulting tied to sales cycles — NOT product engineering"),
        new MismatchExample("Manual QA",
            "manual test execution without automation — NOT SDET / QA Automation"),
        new MismatchExample("Database Administrator (DBA)",
            "database operations / backups / tuning — NOT application engineering"),
        new MismatchExample("IT Support / Helpdesk",
            "user support / ticket handling — NOT product engineering"),
    };

    private static readonly IReadOnlyList<CareerPattern> _careerPatterns = new[]
    {
        new CareerPattern(
            FromRole: "Backend Developer",
            ToRole:   "Fullstack Developer",
            RequiredSignals: new[] { "any frontend project", "React or Vue exposure" },
            ScoreIfSignalsPresent: 0,
            ScoreIfSignalsAbsent:  -5,
            Note: "Natural transition; minor penalty when zero frontend signal."),

        new CareerPattern(
            FromRole: "Backend Developer",
            ToRole:   "DevOps / SRE",
            RequiredSignals: new[] { "Linux", "Docker", "CI/CD pipeline", "cloud (AWS or GCP or Azure)" },
            ScoreIfSignalsPresent: -3,
            ScoreIfSignalsAbsent:  -10,
            Note: "Requires hands-on infra tooling; without signals → moderate gap."),

        new CareerPattern(
            FromRole: "Manual QA",
            ToRole:   "SDET / QA Automation",
            RequiredSignals: new[] { "programming language (Java, Python, C#, JS)",
                                     "test automation framework" },
            ScoreIfSignalsPresent: -3,
            ScoreIfSignalsAbsent:  -12,
            Note: "Programming capability is the differentiator."),

        new CareerPattern(
            FromRole: "Senior IC Engineer",
            ToRole:   "Engineering Manager",
            RequiredSignals: new[] { "team lead", "mentorship", "1:1s", "managed N reports" },
            ScoreIfSignalsPresent: -2,
            ScoreIfSignalsAbsent:  -8,
            Note: "Management track requires explicit leadership evidence."),
    };


    private static readonly IReadOnlyDictionary<string, ToolWeight> _toolWeights =
        new Dictionary<string, ToolWeight>(StringComparer.OrdinalIgnoreCase)
        {

            ["Docker"]      = ToolWeight.Hard,
            ["Kubernetes"]  = ToolWeight.Hard,
            ["Terraform"]   = ToolWeight.Hard,
            ["Helm"]        = ToolWeight.Hard,
            ["Kafka"]       = ToolWeight.Hard,
            ["Redis"]       = ToolWeight.Hard,
            ["gRPC"]        = ToolWeight.Hard,
            ["GraphQL"]     = ToolWeight.Hard,
            ["PostgreSQL"]  = ToolWeight.Hard,
            ["MongoDB"]     = ToolWeight.Hard,
            ["AWS"]         = ToolWeight.Hard,
            ["GCP"]         = ToolWeight.Hard,
            ["Azure"]       = ToolWeight.Hard,


            ["React"]       = ToolWeight.Hard,
            ["TypeScript"]  = ToolWeight.Hard,
            ["Swift"]       = ToolWeight.Hard,
            ["Kotlin"]      = ToolWeight.Hard,
            ["Python"]      = ToolWeight.Hard,
            ["PyTorch"]     = ToolWeight.Hard,
            ["TensorFlow"]  = ToolWeight.Hard,


            ["VS Code"]     = ToolWeight.Easy,
            ["IntelliJ"]    = ToolWeight.Easy,
            ["Git"]         = ToolWeight.Easy,
            ["npm"]         = ToolWeight.Easy,
            ["yarn"]        = ToolWeight.Easy,
        };


    private static readonly IReadOnlyDictionary<SlotId, SlotContent> _slots =
        new Dictionary<SlotId, SlotContent>
        {


            [SlotId.EngineeringMgrRule] = new SlotContent(
                Text:
                    "STEP 4c — Leadership-role rule:\n" +
                    "  • Engineering Manager / Tech Lead positions ARE valid targets for Senior+\n" +
                    "    engineers with team-lead, mentorship, or N-report signals in CV.\n" +
                    "  • Do NOT auto-cap such matches at 25 — evaluate against IC + lead signals.\n" +
                    "  • Conversely, EM/Tech-Lead vacancies for candidates targeting IC engineering\n" +
                    "    roles (Backend / Frontend / etc.) are caught by the deterministic\n" +
                    "    family mismatch cap (24).",
                Policy: SlotPolicy.Replace),


            [SlotId.FamilyBoost] = new SlotContent(
                Text:
                    "Engineering-family scoring guidance:\n" +
                    "  • Tech-stack match is the PRIMARY qualification. A candidate with adjacent\n" +
                    "    stack (.NET ↔ Java) is acceptable with the framework-adjacency penalty.\n" +
                    "    A candidate with FAR stack (.NET ↔ Python) is a moderate gap.\n" +
                    "  • Junior IC vacancy + Senior IC candidate (overqualified) → score MAX 67.\n" +
                    "  • IC vacancy + candidate with Tech Lead / EM signals → still IC-eligible.\n" +
                    "  • For DevOps / SRE / Platform roles, treat Linux/Docker/CI as REQUIRED\n" +
                    "    technical baseline — generic backend background alone is insufficient.\n" +
                    "  • For ML Engineer roles, treat Python + ML framework (PyTorch/TF) as the\n" +
                    "    PRIMARY qualification — generic Python alone is partial fit.",
                Policy: SlotPolicy.Append),
        };
}
