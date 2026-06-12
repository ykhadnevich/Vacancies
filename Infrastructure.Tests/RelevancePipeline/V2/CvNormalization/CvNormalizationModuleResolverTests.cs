using Application.Common.Enums;
using Application.Common.Interfaces;
using Infrastructure.RelevancePipeline.V2.CvNormalization;

namespace Infrastructure.Tests.RelevancePipeline.V2.CvNormalization;

public class CvNormalizationModuleResolverTests
{
    [Fact]
    public void Throws_OnDuplicateDomainRegistration()
    {
        var modules = new ICvNormalizationModule[]
        {
            new TechCvNormalizationModule(),
            new TechCvNormalizationModule(),
            new GenericCvNormalizationModule()
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new CvNormalizationModuleResolver(modules));

        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("Tech", ex.Message);
    }

    [Fact]
    public void Throws_WhenGenericNotRegistered()
    {
        var modules = new ICvNormalizationModule[] { new TechCvNormalizationModule() };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new CvNormalizationModuleResolver(modules));

        Assert.Contains("Generic", ex.Message);
    }

    [Fact]
    public void Returns_RegisteredModule_ForKnownDomain()
    {
        var tech = new TechCvNormalizationModule();
        var generic = new GenericCvNormalizationModule();
        var resolver = new CvNormalizationModuleResolver(
            new ICvNormalizationModule[] { tech, generic });

        Assert.Same(tech, resolver.For(CvDomain.Tech));
        Assert.Same(generic, resolver.For(CvDomain.Generic));
    }

    [Fact]
    public void FallsBackToGeneric_ForReservedButUnregisteredDomain()
    {
        var tech = new TechCvNormalizationModule();
        var generic = new GenericCvNormalizationModule();
        var resolver = new CvNormalizationModuleResolver(
            new ICvNormalizationModule[] { tech, generic });


        Assert.Same(generic, resolver.For(CvDomain.Healthcare));
        Assert.Same(generic, resolver.For(CvDomain.Legal));
        Assert.Same(generic, resolver.For(CvDomain.Education));
        Assert.Same(generic, resolver.For(CvDomain.Creative));
        Assert.Same(generic, resolver.For(CvDomain.Sales));
        Assert.Same(generic, resolver.For(CvDomain.Finance));
    }
}
