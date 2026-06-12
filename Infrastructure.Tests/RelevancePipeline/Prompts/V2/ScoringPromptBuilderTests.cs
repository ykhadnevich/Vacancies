using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;


public class ScoringPromptBuilderTests
{
    [Fact]
    public void Build_ProducesPromptWithExpectedPmContent()
    {
        var builder = MakeBuilder();

        var ctx = new ScoringPromptContext(
            cvText: "{\"seniority\":\"junior\",\"target_roles\":[\"Product Manager\"]}",
            jobTitle: "Junior PM",
            jobCompany: "Acme",
            jobDescription: "Looking for junior PM with 1+ year experience.",
            roleYears: new RoleWeightedYears(0.0, 0, 0, 0, 0.2, 0, 0, 0));

        var result = builder.Build(ctx);

        Assert.NotNull(result);
        Assert.Equal(RoleFamily.Product, result.DetectedFamily);
        Assert.Equal("v1+pm_v23", result.CompositeVersion);
        Assert.True(result.EstimatedInputTokens > 0);
        Assert.Contains("Junior PM", result.Prompt);
        Assert.Contains("Verdict bands", result.Prompt);
        Assert.Contains("Technical PM role", result.Prompt);
    }

    [Fact]
    public void Build_TokenEstimate_GrowsWithPromptSize()
    {
        var builder = MakeBuilder();

        var shortCtx = MakeCtxWithDescription("short desc");
        var longCtx  = MakeCtxWithDescription(new string('x', 5000));

        var shortResult = builder.Build(shortCtx);
        var longResult  = builder.Build(longCtx);

        Assert.True(longResult.EstimatedInputTokens > shortResult.EstimatedInputTokens,
            "Long job description should produce a higher token estimate.");
    }

    [Fact]
    public void Build_ThrowsOnNullContext()
    {
        var builder = MakeBuilder();
        Assert.Throws<ArgumentNullException>(() => builder.Build(null!));
    }

    private static ScoringPromptBuilder MakeBuilder()
    {
        var router = new FixedProductRoleRouter();
        var resolver = new ScoringModuleResolver(new IScoringModule[] { new PmScoringModule() });
        return new ScoringPromptBuilder(router, resolver, new SlotComposer());
    }

    private static ScoringPromptContext MakeCtxWithDescription(string desc) =>
        new(
            cvText: "{\"seniority\":\"junior\"}",
            jobTitle: "PM",
            jobCompany: "Co",
            jobDescription: desc,
            roleYears: null);
}
