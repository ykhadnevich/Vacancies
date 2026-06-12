using Application.Common.Enums;
using Application.Common.Interfaces;
using Infrastructure.RelevancePipeline.V2.CvNormalization;

namespace Infrastructure.Tests.RelevancePipeline.V2.CvNormalization;

public class CvNormalizationPromptBuilderTests
{
    private static CvNormalizationPromptBuilder MakeBuilder()
    {
        var modules = new ICvNormalizationModule[]
        {
            new TechCvNormalizationModule(),
            new GenericCvNormalizationModule()
        };
        var resolver = new CvNormalizationModuleResolver(modules);
        var router = new KeywordCvDomainRouter();
        return new CvNormalizationPromptBuilder(router, resolver);
    }

    [Fact]
    public void Build_RoutesTechCvToTechModule()
    {
        var builder = MakeBuilder();
        var techCv =
            "Software Engineer with 5 years of C# and React experience. " +
            "Built REST APIs in ASP.NET Core, deployed to AWS via Docker.";

        var result = builder.Build(techCv);

        Assert.Equal(CvDomain.Tech, result.DetectedDomain);
        Assert.Equal("v5_1_confidence+tech_v3", result.CompositeVersion);
    }

    [Fact]
    public void Build_RoutesNonTechCvToGenericModule()
    {
        var builder = MakeBuilder();
        var nonTechCv =
            "Registered Nurse — 6 years ICU experience at Kyiv City Hospital. " +
            "Patient care, IV administration, ventilator management.";

        var result = builder.Build(nonTechCv);

        Assert.Equal(CvDomain.Generic, result.DetectedDomain);
        Assert.Equal("v5_1_confidence+generic_v2", result.CompositeVersion);
    }

    [Fact]
    public void Build_IncludesCvTextInPrompt()
    {
        var builder = MakeBuilder();
        var cv = "UNIQUE_TEST_MARKER_98765 — Software Engineer 3yrs Python.";

        var result = builder.Build(cv);

        Assert.Contains("UNIQUE_TEST_MARKER_98765", result.Prompt);
    }

    [Fact]
    public void Build_ProducesNonEmptyPromptAndPositiveTokenEstimate()
    {
        var builder = MakeBuilder();
        var techCv = "Software Engineer C# .NET React Docker Kubernetes AWS PostgreSQL Git";

        var result = builder.Build(techCv);

        Assert.False(string.IsNullOrEmpty(result.Prompt));
        Assert.True(
            result.EstimatedInputTokens > 100,
            $"Expected >100 tokens for non-trivial prompt, got {result.EstimatedInputTokens}.");
    }

    [Fact]
    public void Build_TechAndGenericProduceDistinguishablePrompts()
    {
        var builder = MakeBuilder();
        var techCv =
            "Software Engineer 5yrs C# .NET React Docker Kubernetes AWS REST API";
        var nonTechCv =
            "Registered Nurse — 6yrs ICU patient care, ventilator management, BSN, CCRN";

        var techResult = builder.Build(techCv);
        var genericResult = builder.Build(nonTechCv);

        Assert.NotEqual(techResult.Prompt, genericResult.Prompt);

        Assert.Contains("junior = 0–1", techResult.Prompt);

        Assert.Contains("Healthcare", genericResult.Prompt);
    }

    [Fact]
    public void CurrentExpectedModelVersionPrefix_TracksCoreVersion()
    {
        var builder = MakeBuilder();


        Assert.Equal(
            $"gemini-cv-normalization-{CvNormalizationPromptCore.Version}+",
            builder.CurrentExpectedModelVersionPrefix);
    }

    [Fact]
    public void Build_DeterministicOnSameInput()
    {
        var builder = MakeBuilder();
        var cv = "Software Engineer C# .NET React Docker Kubernetes AWS Git";

        var first = builder.Build(cv);
        var second = builder.Build(cv);


        Assert.Equal(first.Prompt, second.Prompt);
        Assert.Equal(first.CompositeVersion, second.CompositeVersion);
        Assert.Equal(first.DetectedDomain, second.DetectedDomain);
    }
}
