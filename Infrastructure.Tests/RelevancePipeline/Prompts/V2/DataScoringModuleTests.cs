using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;

public class DataScoringModuleTests
{
    private readonly DataScoringModule _module = new();

    [Fact]
    public void Family_And_Version()
    {
        Assert.Equal(RoleFamily.Data, _module.Family);
        Assert.Equal("data_v1", _module.Version);
    }

    [Fact]
    public void BucketMappings_IncludeAnalystsAndEngineers()
    {
        var ids = _module.GetBucketMappings().Select(m => m.Bucket).ToHashSet();
        Assert.Contains(RoleBucketId.DataAnalyst,   ids);
        Assert.Contains(RoleBucketId.DataEngineer,  ids);
    }

    [Fact]
    public void AdjacencyRules_TableauPowerBi_HasSmallPenalty()
    {
        var tp = _module.GetAdjacencyRules().First(r =>
            (r.FromTech == "Tableau" && r.ToTech == "Power BI") ||
            (r.FromTech == "Power BI" && r.ToTech == "Tableau"));
        Assert.True(tp.PenaltyMax <= 5, "Tableau↔PowerBI must be a small transition.");
    }

    [Fact]
    public void CareerPattern_DA_to_DS_RequiresStatisticsSignals()
    {
        var p = _module.GetCareerPatterns()
            .First(x => x.FromRole == "Data Analyst" && x.ToRole == "Data Scientist");
        Assert.Contains(p.RequiredSignals, s => s.Contains("statistics", StringComparison.OrdinalIgnoreCase));
        Assert.True(p.ScoreIfSignalsAbsent < p.ScoreIfSignalsPresent,
            "Heavier penalty when stats signals are absent.");
    }

    [Fact]
    public void ToolWeights_SQL_Python_Tableau_AreHard()
    {
        var w = _module.GetToolWeights(MakeCtx());
        Assert.Equal(ToolWeight.Hard, w["SQL"]);
        Assert.Equal(ToolWeight.Hard, w["Python"]);
        Assert.Equal(ToolWeight.Hard, w["Tableau"]);
    }

    [Fact]
    public void ToolWeights_Excel_IsEasy()
    {
        Assert.Equal(ToolWeight.Easy, _module.GetToolWeights(MakeCtx())["Excel"]);
    }

    [Fact]
    public void Slots_FamilyBoost_Append_DataSpecific()
    {
        var slots = _module.GetSlots(MakeCtx());
        Assert.True(slots.ContainsKey(SlotId.FamilyBoost));
        Assert.Equal(SlotPolicy.Append, slots[SlotId.FamilyBoost].Policy);
        Assert.Contains("Data-family", slots[SlotId.FamilyBoost].Text);
    }

    [Fact]
    public void Mismatch_Includes_Architects_AndAnnotators()
    {
        var titles = _module.GetMismatchList().Select(m => m.Title).ToList();
        Assert.Contains(titles, t => t.Contains("Data Architect"));
        Assert.Contains(titles, t => t.Contains("Annotator", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GetCapsLogic_IsNoOp()
    {
        Assert.Equal("NoOpFamilyCaps", _module.GetCapsLogic().GetType().Name);
    }

    [Fact]
    public void Compose_DataModule_ProducesDataFlavoredPrompt()
    {
        var composer = new SlotComposer();
        var prompt = composer.Compose(MakeCtx(), _module);

        Assert.Contains("HARD CAPS",        prompt);
        Assert.Contains("Verdict bands",    prompt);
        Assert.Contains("Data-family",      prompt);
        Assert.Contains("Tableau",          prompt);
        Assert.Contains("Data Architect",   prompt);
    }

    private static ScoringPromptContext MakeCtx() =>
        new(
            cvText: "{\"target_roles\":[\"Data Analyst\"]}",
            jobTitle: "Senior Data Analyst",
            jobCompany: "Acme",
            jobDescription: "SQL, Tableau, statistical analysis, A/B testing.",
            roleYears: null);
}
