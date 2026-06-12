using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;

public class SlotComposerTests
{
    [Fact]
    public void ComposeOne_Append_DefaultPolicy_AppendsModuleAfterCore()
    {
        var result = SlotComposer.ComposeOne(
            coreDefault: "Core text",
            structured:  string.Empty,
            moduleContent: new SlotContent("Module text", SlotPolicy.Append));
        Assert.Equal("Core text\nModule text", result);
    }

    [Fact]
    public void ComposeOne_Prepend_PutsModuleBeforeCore()
    {
        var result = SlotComposer.ComposeOne("Core", string.Empty,
            new SlotContent("Pre", SlotPolicy.Prepend));
        Assert.Equal("Pre\nCore", result);
    }

    [Fact]
    public void ComposeOne_Replace_DropsBothCoreAndStructured()
    {
        var result = SlotComposer.ComposeOne("Core default", "Structured",
            new SlotContent("Only module", SlotPolicy.Replace));
        Assert.Equal("Only module", result);
    }

    [Fact]
    public void ComposeOne_Skip_ReturnsEmpty()
    {
        var result = SlotComposer.ComposeOne("Core", "Structured",
            new SlotContent(string.Empty, SlotPolicy.Skip));
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void ComposeOne_StructuredJoinsCoreDefaultWithNewline()
    {
        var result = SlotComposer.ComposeOne("Core", "Structured part", null);
        Assert.Equal("Core\nStructured part", result);
    }

    [Fact]
    public void ComposeOne_NoModule_NoStructured_ReturnsCoreOnly()
    {
        var result = SlotComposer.ComposeOne("Just core", string.Empty, null);
        Assert.Equal("Just core", result);
    }

    [Fact]
    public void Compose_RejectsUnknownSlotId_FromModule()
    {
        var composer = new SlotComposer();
        var ex = Assert.Throws<InvalidOperationException>(() =>
            composer.Compose(MakeCtx(), new BogusModule()));
        Assert.Contains("S999_FAKE", ex.Message);
        Assert.Contains("unknown SlotId", ex.Message);
    }

    [Fact]
    public void Compose_PMVacancy_ProducesNonEmptyPrompt()
    {
        var composer = new SlotComposer();
        var module = new PmScoringModule();
        var prompt = composer.Compose(MakeCtx(), module);
        Assert.False(string.IsNullOrWhiteSpace(prompt));
        Assert.Contains("HARD CAPS", prompt);
        Assert.Contains("Verdict bands", prompt);
        Assert.Contains("Technical PM role", prompt);
        Assert.Contains("Bonus/Promo Manager", prompt);
        Assert.Contains("Amazon Seller Central", prompt);
    }

    [Fact]
    public void Compose_OmitsPreComputedYears_WhenRoleYearsNull()
    {
        var composer = new SlotComposer();
        var prompt = composer.Compose(MakeCtx(roleYears: null), new PmScoringModule());
        Assert.DoesNotContain("AUTHORITATIVE, DO NOT RECALCULATE", prompt);
        Assert.DoesNotContain("PM/PO weighted years", prompt);
        Assert.Contains("HARD CAPS", prompt);
    }

    [Fact]
    public void Compose_IncludesPreComputedYears_WhenRoleYearsPopulated()
    {
        var composer = new SlotComposer();
        var prompt = composer.Compose(
            MakeCtx(roleYears: new RoleWeightedYears(0.5, 0, 0, 0, 0.2, 0, 0, 0)),
            new PmScoringModule());
        Assert.Contains("AUTHORITATIVE, DO NOT RECALCULATE", prompt);
        Assert.Contains("PM/PO weighted years          = 0.5", prompt);
    }

    private static ScoringPromptContext MakeCtx(RoleWeightedYears? roleYears = null) =>
        new(
            cvText: "{\"seniority\":\"junior\",\"target_roles\":[\"Product Manager\"]}",
            jobTitle: "Junior Product Manager",
            jobCompany: "TechStartup",
            jobDescription: "Looking for junior PM with 1+ year experience. We will train.",
            roleYears: roleYears);

    private sealed class BogusModule : IScoringModule
    {
        public RoleFamily Family => RoleFamily.Product;
        public string Version => "bogus_v0";
        public IReadOnlyDictionary<SlotId, SlotContent> GetSlots(ScoringPromptContext ctx) =>
            new Dictionary<SlotId, SlotContent>
            {
                [new SlotId("S999_FAKE")] = new SlotContent("nope", SlotPolicy.Append)
            };
        public IReadOnlyList<RoleBucketMapping> GetBucketMappings() => Array.Empty<RoleBucketMapping>();
        public IReadOnlyList<AdjacencyRule> GetAdjacencyRules() => Array.Empty<AdjacencyRule>();
        public IReadOnlyList<MismatchExample> GetMismatchList() => Array.Empty<MismatchExample>();
        public IReadOnlyList<CareerPattern> GetCareerPatterns() => Array.Empty<CareerPattern>();
        public IReadOnlyDictionary<string, ToolWeight> GetToolWeights(ScoringPromptContext ctx) =>
            new Dictionary<string, ToolWeight>();
        public IFamilyCaps GetCapsLogic() => NoOpFamilyCaps.Instance;
    }
}
