using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;

public class DesignScoringModuleTests
{
    private readonly DesignScoringModule _module = new();

    [Fact]
    public void Family_And_Version()
    {
        Assert.Equal(RoleFamily.Design, _module.Family);
        Assert.Equal("design_v1", _module.Version);
    }

    [Fact]
    public void BucketMappings_AllPointToDesignerBucket()
    {
        var mappings = _module.GetBucketMappings();
        Assert.NotEmpty(mappings);
        Assert.All(mappings, m => Assert.Equal(RoleBucketId.Designer, m.Bucket));
    }

    [Fact]
    public void AdjacencyRules_Figma_Sketch_HasSmallPenalty()
    {
        var fs = _module.GetAdjacencyRules().First(r =>
            (r.FromTech == "Figma" && r.ToTech == "Sketch") ||
            (r.FromTech == "Sketch" && r.ToTech == "Figma"));
        Assert.True(fs.PenaltyMax <= 5, "Figma↔Sketch must be a small adjacency penalty.");
    }

    [Fact]
    public void Mismatch_Includes_VisualDesigner_And3DArtist()
    {
        var titles = _module.GetMismatchList().Select(m => m.Title).ToList();
        Assert.Contains(titles, t => t.Contains("Visual Designer"));
        Assert.Contains(titles, t => t.Contains("3D Artist"));
    }

    [Fact]
    public void CareerPattern_UI_To_UX_RequiresResearchSignals()
    {
        var pattern = _module.GetCareerPatterns()
            .First(p => p.FromRole == "UI Designer" && p.ToRole == "UX Designer");

        Assert.Contains(pattern.RequiredSignals, s => s.Contains("research", StringComparison.OrdinalIgnoreCase));
        Assert.True(pattern.ScoreIfSignalsAbsent < pattern.ScoreIfSignalsPresent,
            "Heavier penalty when research signals absent.");
    }

    [Fact]
    public void CareerPattern_Graphic_To_UI_HeavierPenalty_WhenNoPortfolio()
    {
        var pattern = _module.GetCareerPatterns()
            .First(p => p.FromRole == "Graphic Designer" && p.ToRole == "UI Designer");

        Assert.True(Math.Abs(pattern.ScoreIfSignalsAbsent) >= 8,
            "Graphic→UI without portfolio must penalize ≥8 points.");
    }

    [Fact]
    public void ToolWeights_Figma_Sketch_XD_AreHard()
    {
        var w = _module.GetToolWeights(MakeCtx());
        Assert.Equal(ToolWeight.Hard, w["Figma"]);
        Assert.Equal(ToolWeight.Hard, w["Sketch"]);
        Assert.Equal(ToolWeight.Hard, w["Adobe XD"]);
    }

    [Fact]
    public void ToolWeights_Miro_FigJam_AreEasy()
    {
        var w = _module.GetToolWeights(MakeCtx());
        Assert.Equal(ToolWeight.Easy, w["Miro"]);
        Assert.Equal(ToolWeight.Easy, w["FigJam"]);
    }

    [Fact]
    public void Slots_FamilyBoost_EmphasizesPortfolio()
    {
        var slots = _module.GetSlots(MakeCtx());
        Assert.True(slots.ContainsKey(SlotId.FamilyBoost));
        Assert.Equal(SlotPolicy.Append, slots[SlotId.FamilyBoost].Policy);
        Assert.Contains("PORTFOLIO", slots[SlotId.FamilyBoost].Text);
    }

    [Fact]
    public void GetCapsLogic_IsNoOp()
    {
        Assert.Equal("NoOpFamilyCaps", _module.GetCapsLogic().GetType().Name);
    }

    [Fact]
    public void Compose_DesignModule_ProducesPortfolioFlavoredPrompt()
    {
        var composer = new SlotComposer();
        var prompt = composer.Compose(MakeCtx(), _module);

        Assert.Contains("HARD CAPS",       prompt);
        Assert.Contains("Verdict bands",   prompt);
        Assert.Contains("PORTFOLIO",       prompt);
        Assert.Contains("Figma",           prompt);
        Assert.Contains("Visual Designer", prompt);
    }

    private static ScoringPromptContext MakeCtx() =>
        new(
            cvText: "{\"target_roles\":[\"Product Designer\"]}",
            jobTitle: "Senior Product Designer",
            jobCompany: "Acme",
            jobDescription: "Design end-to-end product flows. Figma, user research, prototyping.",
            roleYears: null);
}
