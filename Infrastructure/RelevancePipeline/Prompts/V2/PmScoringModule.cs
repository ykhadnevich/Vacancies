using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class PmScoringModule : IScoringModule
{
    public RoleFamily Family => RoleFamily.Product;
    public string Version => "pm_v23";

    private readonly PmFamilyCaps _caps = new();

    public IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx)
    {


        var slots = new Dictionary<SlotId, SlotContent>
        {


            [SlotId.FamilyBoost] = new SlotContent(TechnicalPmBoost, SlotPolicy.Append),


            [SlotId.CareerSwitcherFam] = new SlotContent(PmCareerSwitcherContext, SlotPolicy.Append),


            [SlotId.PlatformToolsList] = new SlotContent(PmPlatformToolsList, SlotPolicy.Append),
        };
        return slots;
    }

    public IReadOnlyList<RoleBucketMapping> GetBucketMappings() => _bucketMappings;

    public IReadOnlyList<AdjacencyRule> GetAdjacencyRules() => Array.Empty<AdjacencyRule>();

    public IReadOnlyList<MismatchExample> GetMismatchList() => _mismatchExamples;

    public IReadOnlyList<CareerPattern> GetCareerPatterns() => Array.Empty<CareerPattern>();

    public IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx) => _toolWeights;

    public IFamilyCaps GetCapsLogic() => _caps;


    private static readonly IReadOnlyList<RoleBucketMapping> _bucketMappings = new[]
    {
        new RoleBucketMapping("Product Manager / Product Owner / Head of Product",
            RoleBucketId.PmPo),
        new RoleBucketMapping("Product Marketing Manager / Growth Manager",
            RoleBucketId.Pmm),
        new RoleBucketMapping("Business Analyst / Systems Analyst",
            RoleBucketId.BusinessAnalyst),
        new RoleBucketMapping("Project Manager / Program Manager",
            RoleBucketId.ProjectManager),
        new RoleBucketMapping("Developer / Engineer",
            RoleBucketId.Developer),
    };

    private static readonly IReadOnlyList<MismatchExample> _mismatchExamples = new[]
    {
        new MismatchExample("Bonus/Promo Manager",
            "gambling bonus mechanics, wagering, promotion rules — NOT product management"),
        new MismatchExample("Growth Operations",
            "account farming, LinkedIn automation, ban bypass — NOT product management"),
        new MismatchExample("LiveOps Manager",
            "live game events, game economy, game operations — NOT product management"),
        new MismatchExample("FMCG Product Manager",
            "physical goods, packaging, supply chain, production — NOT software product management"),
        new MismatchExample("Account / Sales / Content Manager",
            "client-facing or content production — NOT product management"),
    };

    private static readonly IReadOnlyDictionary<string, ToolWeight> _toolWeights =
        new Dictionary<string, ToolWeight>(StringComparer.OrdinalIgnoreCase)
        {

            ["SQL"]       = ToolWeight.Hard,
            ["Python"]    = ToolWeight.Hard,
            ["Amplitude"] = ToolWeight.Hard,
            ["Mixpanel"]  = ToolWeight.Hard,
            ["Tableau"]   = ToolWeight.Hard,
            ["Power BI"]  = ToolWeight.Hard,
            ["Looker"]    = ToolWeight.Hard,
            ["Firebase"]  = ToolWeight.Hard,
            ["Google Ads"] = ToolWeight.Hard,
            ["Meta Ads"]  = ToolWeight.Hard,


            ["Jira"]       = ToolWeight.Easy,
            ["Confluence"] = ToolWeight.Easy,
            ["Notion"]     = ToolWeight.Easy,
            ["Miro"]       = ToolWeight.Easy,
            ["Figma"]      = ToolWeight.Easy,
        };


    private const string TechnicalPmBoost =
        "Technical PM role — developer-turned-PM scoring:\n" +
        "  Definition: job requires or strongly values software development background\n" +
        "  (ERP, B2B SaaS, dev tools, API/platform products, fintech backend, data products,\n" +
        "   integration platforms, technical product ownership).\n" +
        "\n" +
        "  If the job IS a technical PM role AND the candidate has C#/.NET, React, SQL, REST API,\n" +
        "  Docker, CI/CD, ASP.NET Core → this technical background IS the PRIMARY qualification.\n" +
        "  Do NOT treat it as 'nice to have' — it is exactly what the job requires.\n" +
        "\n" +
        "  Concrete scoring for technical PM + developer-turned-PM:\n" +
        "  • Strong technical match + 0yr PM experience + junior-friendly job → score 58-68, partial_fit\n" +
        "  • Strong technical match + 0yr PM experience + 1yr requirement → score 52-62, partial_fit\n" +
        "  • Strong technical match + 0yr PM experience + 2yr requirement → score 40-52, partial_fit\n" +
        "  • NO technical match required (pure B2C, marketing PM, growth) → apply standard caps\n" +
        "\n" +
        "  In gaps, list missing domain knowledge (ERP specifics, payment systems) NOT the\n" +
        "  technical skills the candidate already has. Never list 'no PM experience' as a gap\n" +
        "  when the job's primary requirement is technical background that the candidate has.";

    private const string PmCareerSwitcherContext =
        "  For the Product family: career_switcher=true + has_real_product_experience=false\n" +
        "  typically = developer transitioning to PM. This is a known, legitimate path. Many\n" +
        "  companies hire exactly this profile for technical PM roles.";

    private const string PmPlatformToolsList =
        "  Platform tools for Product family (always critical if explicitly required, no \"eager to learn\"):\n" +
        "    Amazon: Amazon Seller Central, Amazon SEO, Helium 10, Amazon Brand Analytics\n" +
        "    Advertising: Google Ads Manager, Meta Ads Manager (hands-on campaign management)\n" +
        "    Analytics: Mixpanel, Tableau, Power BI, Looker, Firebase (when stated as primary tool)\n" +
        "    e.g. do NOT add 'Amazon Seller Central' to a POS or SaaS PM job — it must be EXPLICIT.";
}
