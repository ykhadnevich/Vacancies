using Application.Common.Enums;
using Infrastructure.RelevancePipeline.V2.CvNormalization;

namespace Infrastructure.Tests.RelevancePipeline.V2.CvNormalization;

public class KeywordCvDomainRouterTests
{
    [Fact]
    public void Detects_Tech_OnSoftwareCv()
    {
        var router = new KeywordCvDomainRouter();
        var cv =
            "Software Engineer with 5 years of C# and React experience. " +
            "Built REST APIs in ASP.NET Core, deployed to AWS via Docker. " +
            "Used Kubernetes, PostgreSQL, and Git for daily work.";

        var result = router.Detect(cv);

        Assert.Equal(CvDomain.Tech, result.Domain);
        Assert.True(result.Confidence > 0, "Tech CV should have positive confidence.");
    }

    [Fact]
    public void FallsBackToGeneric_OnNonTechCv()
    {
        var router = new KeywordCvDomainRouter();
        var cv =
            "Registered Nurse — 6 years ICU experience at Kyiv City Hospital. " +
            "Patient care, IV administration, ventilator management, post-op " +
            "monitoring. BSN, CCRN certifications.";

        var result = router.Detect(cv);

        Assert.Equal(CvDomain.Generic, result.Domain);
        Assert.Equal(1.0, result.Confidence);
    }

    [Fact]
    public void FallsBackToGeneric_OnEmptyInput()
    {
        var router = new KeywordCvDomainRouter();

        var result = router.Detect("");

        Assert.Equal(CvDomain.Generic, result.Domain);
    }

    [Fact]
    public void FallsBackToGeneric_OnWhitespaceInput()
    {
        var router = new KeywordCvDomainRouter();

        var result = router.Detect("   \n\t  ");

        Assert.Equal(CvDomain.Generic, result.Domain);
    }

    [Fact]
    public void FallsBackToGeneric_BelowMinAbsoluteScore()
    {
        var router = new KeywordCvDomainRouter();

        var cv = "Sales Manager at retail company. Used SQL once for quarterly reports.";

        var result = router.Detect(cv);

        Assert.Equal(CvDomain.Generic, result.Domain);
    }

    [Fact]
    public void Detects_Tech_OnUkrainianKeywords()
    {
        var router = new KeywordCvDomainRouter();
        var cv =
            "Я програміст з досвідом 3 роки. Працював розробником у IT-компанії. " +
            "Девелопер C# та .NET, інженер-програміст з бекенд-фокусом.";

        var result = router.Detect(cv);

        Assert.Equal(CvDomain.Tech, result.Domain);
    }

    [Fact]
    public void DenseTechCv_HasHigherConfidenceThanSparse()
    {
        var router = new KeywordCvDomainRouter();


        var dense = "C# React Docker Kubernetes AWS PostgreSQL Git REST API Python";

        var sparse =
            "C# React Docker Kubernetes AWS PostgreSQL Git REST API Python " +
            string.Join(" ", Enumerable.Repeat("lorem ipsum dolor sit amet", 100));

        var denseResult = router.Detect(dense);
        var sparseResult = router.Detect(sparse);

        Assert.Equal(CvDomain.Tech, denseResult.Domain);
        Assert.Equal(CvDomain.Tech, sparseResult.Domain);
        Assert.True(
            denseResult.Confidence > sparseResult.Confidence,
            $"Expected dense ({denseResult.Confidence:F3}) > sparse " +
            $"({sparseResult.Confidence:F3}) confidence.");
    }
}
