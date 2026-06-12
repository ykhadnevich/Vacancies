using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;

namespace Infrastructure.RelevancePipeline.Prompts.V2;


public sealed class DesignScoringModule : IScoringModule
{
    public RoleFamily Family => RoleFamily.Design;
    public string Version => "design_v1";

    public IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx) => _slots;
    public IReadOnlyList<RoleBucketMapping> GetBucketMappings() => _bucketMappings;
    public IReadOnlyList<AdjacencyRule>     GetAdjacencyRules() => _adjacencyRules;
    public IReadOnlyList<MismatchExample>   GetMismatchList()   => _mismatchExamples;
    public IReadOnlyList<CareerPattern>     GetCareerPatterns() => _careerPatterns;
    public IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx) => _toolWeights;
    public IFamilyCaps GetCapsLogic() => NoOpFamilyCaps.Instance;


    private static readonly IReadOnlyList<RoleBucketMapping> _bucketMappings = new[]
    {
        new RoleBucketMapping("UX / UI / Product Designer",
            RoleBucketId.Designer,
            Note: "Aggregate Design bucket — all visual/interaction roles map here in v1."),
        new RoleBucketMapping("Graphic / Motion / Brand Designer",
            RoleBucketId.Designer,
            Note: "Same bucket; sub-type differentiation handled via tool weights."),
    };

    private static readonly IReadOnlyList<AdjacencyRule> _adjacencyRules = new[]
    {
        new AdjacencyRule("Figma", "Sketch", 2, 4, AdjacencyDirection.Symmetric,
            Note: "Same conceptual model — vector-first UI design with components."),
        new AdjacencyRule("Figma", "Adobe XD", 2, 4, AdjacencyDirection.Symmetric,
            Note: "Adobe XD has different shortcut model but same workflow."),
        new AdjacencyRule("Sketch", "Adobe XD", 3, 5, AdjacencyDirection.Symmetric),
        new AdjacencyRule("After Effects", "Premiere Pro", 3, 6, AdjacencyDirection.Symmetric,
            Note: "Adobe ecosystem overlap; motion vs editing focus differs."),
        new AdjacencyRule("Photoshop", "Illustrator", 2, 4, AdjacencyDirection.Symmetric,
            Note: "Same vendor; raster vs vector specialisation."),
    };

    private static readonly IReadOnlyList<MismatchExample> _mismatchExamples = new[]
    {
        new MismatchExample("Visual Designer (print/marketing)",
            "marketing collateral / banners / print — NOT product UX work"),
        new MismatchExample("Graphic Designer (when target = Product Designer)",
            "static branding assets — NOT end-to-end product flows + research"),
        new MismatchExample("3D Artist / Game Artist",
            "3D modeling / texturing for games — NOT 2D product design"),
        new MismatchExample("Illustrator (artist)",
            "editorial / character illustration — NOT product UI work"),
        new MismatchExample("Web Developer who 'does design'",
            "HTML/CSS implementation, not design discipline"),
    };

    private static readonly IReadOnlyList<CareerPattern> _careerPatterns = new[]
    {
        new CareerPattern(
            FromRole: "Graphic Designer",
            ToRole:   "UI Designer",
            RequiredSignals: new[] { "Figma or Sketch portfolio", "responsive design",
                                     "component library work" },
            ScoreIfSignalsPresent: -3,
            ScoreIfSignalsAbsent:  -10,
            Note: "Graphic→UI transition requires product-tooling portfolio evidence."),

        new CareerPattern(
            FromRole: "UI Designer",
            ToRole:   "UX Designer",
            RequiredSignals: new[] { "user research", "usability testing", "wireframing",
                                     "information architecture" },
            ScoreIfSignalsPresent: -2,
            ScoreIfSignalsAbsent:  -8,
            Note: "UX requires research and discovery signals — UI craft alone is partial."),

        new CareerPattern(
            FromRole: "UX Designer",
            ToRole:   "Product Designer",
            RequiredSignals: new[] { "end-to-end shipped feature", "cross-functional collaboration",
                                     "metrics impact" },
            ScoreIfSignalsPresent: -1,
            ScoreIfSignalsAbsent:  -6,
            Note: "Product Designer scope expands UX with business outcome ownership."),
    };

    private static readonly IReadOnlyDictionary<string, ToolWeight> _toolWeights =
        new Dictionary<string, ToolWeight>(StringComparer.OrdinalIgnoreCase)
        {

            ["Figma"]         = ToolWeight.Hard,
            ["Sketch"]        = ToolWeight.Hard,
            ["Adobe XD"]      = ToolWeight.Hard,
            ["Framer"]        = ToolWeight.Hard,
            ["ProtoPie"]      = ToolWeight.Hard,
            ["Principle"]     = ToolWeight.Hard,

            ["Photoshop"]     = ToolWeight.Hard,
            ["Illustrator"]   = ToolWeight.Hard,
            ["After Effects"] = ToolWeight.Hard,
            ["InDesign"]      = ToolWeight.Hard,
            ["Premiere Pro"]  = ToolWeight.Hard,

            ["Blender"]       = ToolWeight.Hard,
            ["Cinema 4D"]     = ToolWeight.Hard,


            ["Miro"]          = ToolWeight.Easy,
            ["FigJam"]        = ToolWeight.Easy,
            ["Notion"]        = ToolWeight.Easy,
            ["Zeplin"]        = ToolWeight.Easy,
            ["Abstract"]      = ToolWeight.Easy,
        };

    private static readonly IReadOnlyDictionary<SlotId, SlotContent> _slots =
        new Dictionary<SlotId, SlotContent>
        {
            [SlotId.FamilyBoost] = new SlotContent(
                Text:
                    "Design-family scoring guidance:\n" +
                    "  • A PORTFOLIO is the PRIMARY signal. If the CV mentions a portfolio link\n" +
                    "    or specific shipped designs → treat as strong qualification evidence.\n" +
                    "  • If the vacancy EXPLICITLY asks for a portfolio and the CV shows no\n" +
                    "    portfolio link or shipped-design evidence → critical gap.\n" +
                    "  • Figma is the modern standard tool. Sketch / Adobe XD candidates can\n" +
                    "    convert with the framework-adjacency penalty (2-4 points).\n" +
                    "  • UX Designer roles require research / usability testing signals — pure\n" +
                    "    UI craft is partial fit for UX positions.\n" +
                    "  • Product Designer roles expand UX with end-to-end ownership: design +\n" +
                    "    metrics impact + cross-functional collaboration.\n" +
                    "  • Graphic / Motion / Print backgrounds are NOT direct UX/Product Design\n" +
                    "    fits — apply career-pattern penalty when transitioning.",
                Policy: SlotPolicy.Append),
        };
}
