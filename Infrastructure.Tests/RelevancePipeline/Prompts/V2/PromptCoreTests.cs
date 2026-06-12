using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;


public class PromptCoreTests
{
    private static ScoringPromptContext MakeCtx(RoleWeightedYears? years = null) =>
        new(
            cvText: "{\"seniority\":\"junior\",\"target_roles\":[\"Product Manager\"]}",
            jobTitle: "Product Manager",
            jobCompany: "Acme",
            jobDescription: "We are looking for a junior PM with 1+ year of experience.",
            roleYears: years);

    [Fact]
    public void BuildDefault_ReturnsNonNull_ForAllRegisteredSlots()
    {
        var ctx = MakeCtx(new RoleWeightedYears(0.5, 0, 0, 0, 0.2, 0, 0, 0));
        foreach (var slotId in SlotId.AllInOrder)
        {
            var content = PromptCore.BuildDefault(slotId, ctx);
            Assert.NotNull(content);
        }
    }

    [Fact]
    public void BuildDefault_ThrowsForUnknownSlot()
    {
        var ctx = MakeCtx();
        var unknown = new SlotId("S999_FAKE");
        Assert.Throws<ArgumentException>(() => PromptCore.BuildDefault(unknown, ctx));
    }

    [Fact]
    public void Header_ContainsRoleIntroAndCandidateAndJob()
    {
        var ctx = MakeCtx();
        var header = PromptCore.BuildDefault(SlotId.Header, ctx);

        Assert.Contains("senior HR analyst", header);
        Assert.Contains("Candidate profile", header);
        Assert.Contains("Product Manager", header);
        Assert.Contains("Acme", header);
        Assert.Contains("junior PM with 1+ year", header);
    }

    [Fact]
    public void Header_DropsCompanyLineWhenEmpty()
    {
        var ctx = new ScoringPromptContext(
            cvText: "{\"seniority\":\"junior\"}",
            jobTitle: "PM",
            jobCompany: "",
            jobDescription: "desc",
            roleYears: null);

        var header = PromptCore.BuildDefault(SlotId.Header, ctx);
        Assert.DoesNotContain("Company:", header);
        Assert.Contains("Title: PM", header);
    }

    [Fact]
    public void Header_AddsCvNoteForRawText()
    {
        var ctx = new ScoringPromptContext(
            cvText: "John Doe, Software Engineer with 5 years experience...",
            jobTitle: "PM",
            jobCompany: "X",
            jobDescription: "d",
            roleYears: null);

        var header = PromptCore.BuildDefault(SlotId.Header, ctx);
        Assert.Contains("CV above is raw text", header);
    }

    [Fact]
    public void PreComputedYears_IsEmptyWhenRoleYearsNull()
    {
        var ctx = MakeCtx(years: null);
        var content = PromptCore.BuildDefault(SlotId.PreComputedYears, ctx);
        Assert.Equal(string.Empty, content);
    }

    [Fact]
    public void PreComputedYears_RendersAllEightBuckets_WhenPopulated()
    {
        var ctx = MakeCtx(new RoleWeightedYears(
            PmPo: 1.5, Pmm: 0.0, BusinessAnalyst: 0.7, ProjectManager: 0.0,
            Developer: 2.3, DataAnalyst: 0.0, Designer: 0.0, Marketing: 0.0));
        var content = PromptCore.BuildDefault(SlotId.PreComputedYears, ctx);

        Assert.Contains("PM/PO weighted years          = 1.5", content);
        Assert.Contains("Business Analyst weighted yrs = 0.7", content);
        Assert.Contains("Developer/Engineer weighted   = 2.3", content);
        Assert.Contains("Use these numbers in STEP 2", content);
    }

    [Fact]
    public void HardCaps_AreFullyPresentInDefaults()
    {
        var ctx = MakeCtx();
        Assert.Contains("HARD CAPS",     PromptCore.BuildDefault(SlotId.HardCapsStep1, ctx));
        Assert.Contains("STEP 1",        PromptCore.BuildDefault(SlotId.HardCapsStep1, ctx));
        Assert.Contains("STEP 2",        PromptCore.BuildDefault(SlotId.HardCapsStep2Map, ctx));
        Assert.Contains("STEP 3",        PromptCore.BuildDefault(SlotId.HardCapsStep3, ctx));
        Assert.Contains("STEP 4",        PromptCore.BuildDefault(SlotId.MidSeniorJuniorCap, ctx));
        Assert.Contains("Engineering Manager", PromptCore.BuildDefault(SlotId.EngineeringMgrRule, ctx));
    }

    [Fact]
    public void VerdictBands_ContainsAllFourVerdicts()
    {
        var content = PromptCore.BuildDefault(SlotId.VerdictBands, MakeCtx());
        Assert.Contains("strong_fit",  content);
        Assert.Contains("good_fit",    content);
        Assert.Contains("partial_fit", content);
        Assert.Contains("weak_fit",    content);
    }

    [Fact]
    public void ModuleOwnedSlots_AreEmpty_InCoreDefault()
    {
        var ctx = MakeCtx();
        Assert.Equal(string.Empty, PromptCore.BuildDefault(SlotId.MismatchExamples,  ctx));
        Assert.Equal(string.Empty, PromptCore.BuildDefault(SlotId.FamilyBoost,       ctx));
        Assert.Equal(string.Empty, PromptCore.BuildDefault(SlotId.CareerSwitcherFam, ctx));
        Assert.Equal(string.Empty, PromptCore.BuildDefault(SlotId.PlatformToolsList, ctx));
        Assert.Equal(string.Empty, PromptCore.BuildDefault(SlotId.ToolWeightList,    ctx));
    }

    [Fact]
    public void Version_IsStable_v1()
    {
        Assert.Equal("v1", PromptCore.Version);
    }
}
