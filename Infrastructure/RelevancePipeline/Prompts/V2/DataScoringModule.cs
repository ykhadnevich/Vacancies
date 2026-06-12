using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class DataScoringModule : IScoringModule
{
    public RoleFamily Family => RoleFamily.Data;
    public string Version => "data_v1";

    public IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx) => _slots;
    public IReadOnlyList<RoleBucketMapping> GetBucketMappings() => _bucketMappings;
    public IReadOnlyList<AdjacencyRule>     GetAdjacencyRules() => _adjacencyRules;
    public IReadOnlyList<MismatchExample>   GetMismatchList()   => _mismatchExamples;
    public IReadOnlyList<CareerPattern>     GetCareerPatterns() => _careerPatterns;
    public IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx) => _toolWeights;
    public IFamilyCaps GetCapsLogic() => NoOpFamilyCaps.Instance;


    private static readonly IReadOnlyList<RoleBucketMapping> _bucketMappings = new[]
    {
        new RoleBucketMapping("Data Analyst / BI Analyst / Analytics",
            RoleBucketId.DataAnalyst,
            Note: "Includes Business Intelligence Analyst — same skill stack."),
        new RoleBucketMapping("Data Scientist / Senior Data Analyst with ML focus",
            RoleBucketId.DataAnalyst,
            Note: "Aggregated with DataAnalyst bucket in v1 — ML-focus signals captured via tool weights."),
        new RoleBucketMapping("Data Engineer / Analytics Engineer",
            RoleBucketId.DataEngineer),
    };

    private static readonly IReadOnlyList<AdjacencyRule> _adjacencyRules = new[]
    {
        new AdjacencyRule("Tableau", "Power BI", 2, 4, AdjacencyDirection.Symmetric,
            Note: "Same conceptual model, different vendor UI."),
        new AdjacencyRule("Tableau", "Looker", 3, 6, AdjacencyDirection.Symmetric,
            Note: "Looker has a different model (LookML)."),
        new AdjacencyRule("Power BI", "Looker", 4, 7, AdjacencyDirection.Symmetric),
        new AdjacencyRule("Python", "R", 5, 9, AdjacencyDirection.Symmetric,
            Note: "Different ecosystems; statistical literacy transfers."),
    };

    private static readonly IReadOnlyList<MismatchExample> _mismatchExamples = new[]
    {
        new MismatchExample("Business Analyst (Product family)",
            "requirements gathering for product features — NOT data analysis (different family)"),
        new MismatchExample("Data Architect",
            "long-horizon data modeling at organization level — NOT IC analytics work"),
        new MismatchExample("Marketing Analyst",
            "marketing attribution / campaign analytics — overlaps but distinct skill stack"),
        new MismatchExample("Data Annotator / Labeller",
            "manual data labelling for ML training — NOT analytical role"),
    };

    private static readonly IReadOnlyList<CareerPattern> _careerPatterns = new[]
    {
        new CareerPattern(
            FromRole: "BI Analyst",
            ToRole:   "Data Analyst",
            RequiredSignals: new[] { "advanced SQL", "Python or R", "experimentation basics" },
            ScoreIfSignalsPresent: -2,
            ScoreIfSignalsAbsent:  -6,
            Note: "Natural progression; programming literacy is the differentiator."),

        new CareerPattern(
            FromRole: "Data Analyst",
            ToRole:   "Data Scientist",
            RequiredSignals: new[] { "statistics / hypothesis testing", "Python / pandas / sklearn",
                                     "machine learning project" },
            ScoreIfSignalsPresent: -3,
            ScoreIfSignalsAbsent:  -12,
            Note: "Data Scientist requires explicit ML / stats portfolio."),

        new CareerPattern(
            FromRole: "Data Analyst",
            ToRole:   "Data Engineer",
            RequiredSignals: new[] { "ETL pipeline experience", "dbt or Airflow", "Spark or warehouse internals" },
            ScoreIfSignalsPresent: -3,
            ScoreIfSignalsAbsent:  -10,
            Note: "Data Engineering is a separate craft — requires infra signals."),
    };

    private static readonly IReadOnlyDictionary<string, ToolWeight> _toolWeights =
        new Dictionary<string, ToolWeight>(StringComparer.OrdinalIgnoreCase)
        {

            ["SQL"]        = ToolWeight.Hard,
            ["Python"]     = ToolWeight.Hard,
            ["R"]          = ToolWeight.Hard,
            ["Tableau"]    = ToolWeight.Hard,
            ["Power BI"]   = ToolWeight.Hard,
            ["Looker"]     = ToolWeight.Hard,
            ["dbt"]        = ToolWeight.Hard,
            ["Airflow"]    = ToolWeight.Hard,
            ["Spark"]      = ToolWeight.Hard,
            ["Snowflake"]  = ToolWeight.Hard,
            ["BigQuery"]   = ToolWeight.Hard,
            ["Redshift"]   = ToolWeight.Hard,
            ["pandas"]     = ToolWeight.Hard,
            ["scikit-learn"] = ToolWeight.Hard,
            ["PyTorch"]    = ToolWeight.Hard,
            ["TensorFlow"] = ToolWeight.Hard,
            ["Jupyter"]    = ToolWeight.Hard,


            ["Excel"]      = ToolWeight.Easy,
            ["Google Sheets"] = ToolWeight.Easy,
            ["Notion"]     = ToolWeight.Easy,
            ["Confluence"] = ToolWeight.Easy,
            ["Git"]        = ToolWeight.Easy,
        };

    private static readonly IReadOnlyDictionary<SlotId, SlotContent> _slots =
        new Dictionary<SlotId, SlotContent>
        {
            [SlotId.FamilyBoost] = new SlotContent(
                Text:
                    "Data-family scoring guidance:\n" +
                    "  • SQL fluency is the PRIMARY qualification. A candidate with strong SQL +\n" +
                    "    domain knowledge is a strong fit even with weaker BI-tool experience.\n" +
                    "  • Data Scientist vacancies require explicit statistics/ML signals — generic\n" +
                    "    'data analyst' background without statistics + Python ML libs is partial fit.\n" +
                    "  • Data Engineer vacancies require infrastructure signals (Airflow / dbt /\n" +
                    "    Spark) — a candidate with only BI tools is a moderate gap.\n" +
                    "  • BI tool migration (Tableau ↔ Power BI) is a small gap — same conceptual\n" +
                    "    model. Looker has a different model (LookML) — slightly larger gap.",
                Policy: SlotPolicy.Append),
        };
}
