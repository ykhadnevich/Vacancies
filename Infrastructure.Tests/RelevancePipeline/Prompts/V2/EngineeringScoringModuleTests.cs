using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;

public class EngineeringScoringModuleTests
{
    private readonly EngineeringScoringModule _module = new();

    [Fact]
    public void Family_And_Version_Are_Engineering()
    {
        Assert.Equal(RoleFamily.Engineering, _module.Family);
        Assert.Equal("eng_v1", _module.Version);
    }

    [Fact]
    public void BucketMappings_Cover_All_9_Subtypes()
    {
        var mappings = _module.GetBucketMappings();
        var ids = mappings.Select(m => m.Bucket).ToHashSet();

        Assert.Contains(RoleBucketId.Backend,      ids);
        Assert.Contains(RoleBucketId.Frontend,     ids);
        Assert.Contains(RoleBucketId.Fullstack,    ids);
        Assert.Contains(RoleBucketId.Mobile,       ids);
        Assert.Contains(RoleBucketId.DevOps,       ids);
        Assert.Contains(RoleBucketId.Qa,           ids);
        Assert.Contains(RoleBucketId.MlEngineer,   ids);
        Assert.Contains(RoleBucketId.DataEngineer, ids);
        Assert.Contains(RoleBucketId.Embedded,     ids);
    }

    [Fact]
    public void AdjacencyRules_Contain_DotNet_Java_Penalty()
    {
        var rules = _module.GetAdjacencyRules();
        var dotnetJava = rules.FirstOrDefault(r =>
            (r.FromTech == ".NET" && r.ToTech == "Java") ||
            (r.FromTech == "Java" && r.ToTech == ".NET"));
        Assert.NotNull(dotnetJava);
        Assert.Equal(4, dotnetJava!.PenaltyMin);
        Assert.Equal(7, dotnetJava.PenaltyMax);
    }

    [Fact]
    public void AdjacencyRules_iOS_Android_Is_Higher_Penalty_Than_React_Vue()
    {
        var rules = _module.GetAdjacencyRules();
        var iosAndroid = rules.First(r =>
            (r.FromTech == "iOS" && r.ToTech == "Android") || (r.FromTech == "Android" && r.ToTech == "iOS"));
        var reactVue = rules.First(r =>
            (r.FromTech == "React" && r.ToTech == "Vue") || (r.FromTech == "Vue" && r.ToTech == "React"));

        Assert.True(iosAndroid.PenaltyMin > reactVue.PenaltyMax,
            "iOS↔Android transition must cost more than React↔Vue.");
    }

    [Fact]
    public void MismatchList_Excludes_EngineeringManager()
    {


        var mismatches = _module.GetMismatchList();
        Assert.DoesNotContain(mismatches, m =>
            m.Title.Contains("Engineering Manager", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MismatchList_Includes_SalesEngineer_AndManualQa()
    {
        var mismatches = _module.GetMismatchList();
        Assert.Contains(mismatches, m => m.Title.Contains("Sales Engineer"));
        Assert.Contains(mismatches, m => m.Title.Contains("Manual QA"));
    }

    [Fact]
    public void CareerPatterns_Backend_To_DevOps_HasSignalConditional()
    {
        var pattern = _module.GetCareerPatterns()
            .First(p => p.FromRole.Contains("Backend") && p.ToRole.Contains("DevOps"));

        Assert.NotEmpty(pattern.RequiredSignals);
        Assert.True(pattern.ScoreIfSignalsAbsent < pattern.ScoreIfSignalsPresent,
            "Penalty must be larger when signals are absent.");
    }

    [Fact]
    public void ToolWeights_Docker_Kubernetes_Are_Hard()
    {
        var weights = _module.GetToolWeights(MakeCtx());
        Assert.Equal(ToolWeight.Hard, weights["Docker"]);
        Assert.Equal(ToolWeight.Hard, weights["Kubernetes"]);
        Assert.Equal(ToolWeight.Hard, weights["Terraform"]);
    }

    [Fact]
    public void ToolWeights_VsCode_IsEasy()
    {
        var weights = _module.GetToolWeights(MakeCtx());
        Assert.Equal(ToolWeight.Easy, weights["VS Code"]);
    }

    [Fact]
    public void Slots_OverrideEngineeringMgrRule_WithReplacePolicy()
    {
        var slots = _module.GetSlots(MakeCtx());

        Assert.True(slots.ContainsKey(SlotId.EngineeringMgrRule));
        var em = slots[SlotId.EngineeringMgrRule];
        Assert.Equal(SlotPolicy.Replace, em.Policy);
        Assert.Contains("ARE valid targets", em.Text);
    }

    [Fact]
    public void Slots_FamilyBoost_AppendsEngineeringGuidance()
    {
        var slots = _module.GetSlots(MakeCtx());

        Assert.True(slots.ContainsKey(SlotId.FamilyBoost));
        var boost = slots[SlotId.FamilyBoost];
        Assert.Equal(SlotPolicy.Append, boost.Policy);
        Assert.Contains("Engineering-family", boost.Text);
    }

    [Fact]
    public void GetCapsLogic_ReturnsEngFamilyCaps()
    {
        var caps = _module.GetCapsLogic();
        Assert.Equal("EngFamilyCaps", caps.GetType().Name);
    }

    [Fact]
    public void Compose_WithEngModule_ProducesEngineeringFlavoredPrompt()
    {
        var composer = new SlotComposer();
        var prompt = composer.Compose(MakeCtx(), _module);


        Assert.Contains("FRAMEWORK ADJACENCY", prompt);
        Assert.Contains(".NET ↔ Java", prompt);
        Assert.Contains("Sales Engineer", prompt);
        Assert.Contains("Engineering-family scoring guidance", prompt);

        Assert.DoesNotContain("for non-engineers", prompt);

        Assert.Contains("HARD CAPS", prompt);
        Assert.Contains("Verdict bands", prompt);
    }

    private static ScoringPromptContext MakeCtx() =>
        new(
            cvText: "{\"target_roles\":[\"Backend Developer\"]}",
            jobTitle: "Senior Backend Engineer",
            jobCompany: "Acme",
            jobDescription: "Build distributed services in Go. Kubernetes, AWS, PostgreSQL.",
            roleYears: null);
}
