using Application.Common.Interfaces;
using Application.Common.Scoring;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.FamilyCaps;
using Infrastructure.RelevancePipeline.Prompts.V2;

namespace Infrastructure.Tests.RelevancePipeline.Prompts.V2;

public class ScoringModuleResolverTests
{
    [Fact]
    public void Throws_OnDuplicateFamilyRegistration()
    {
        var modules = new IScoringModule[]
        {
            new PmScoringModule(),
            new PmScoringModule(),
        };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ScoringModuleResolver(modules));

        Assert.Contains("Duplicate", ex.Message);
        Assert.Contains("Product", ex.Message);
    }

    [Fact]
    public void Throws_WhenGenericRequired_ButNotRegistered()
    {
        var modules = new IScoringModule[] { new PmScoringModule() };

        var ex = Assert.Throws<InvalidOperationException>(
            () => new ScoringModuleResolver(modules, requireGeneric: true));

        Assert.Contains("Generic", ex.Message);
    }

    [Fact]
    public void Phase0b_AllowsMissingGeneric_WhenNotRequired()
    {
        var modules = new IScoringModule[] { new PmScoringModule() };
        var resolver = new ScoringModuleResolver(modules, requireGeneric: false);


        Assert.Same(modules[0], resolver.For(RoleFamily.Product));
    }

    [Fact]
    public void FallsBackToFirstRegistered_WhenFamilyMissingAndNoGeneric()
    {

        var pm = new PmScoringModule();
        var resolver = new ScoringModuleResolver(new IScoringModule[] { pm });


        Assert.Same(pm, resolver.For(RoleFamily.Engineering));
    }


    private static ScoringModuleResolver MakeFullResolver() =>
        new(new IScoringModule[]
        {
            new PmScoringModule(),
            new EngineeringScoringModule(),
            new DesignScoringModule(),
            new DataScoringModule(),
            new GenericScoringModule(),
        }, requireGeneric: true);


    [Fact]
    public void DevOps_MapsToEngineering_Module()
    {
        var resolver = MakeFullResolver();
        var module = resolver.For(RoleFamily.DevOps);

        Assert.IsType<EngineeringScoringModule>(module);
    }


    [Fact]
    public void Marketing_MapsToGeneric_Module()
    {
        var resolver = MakeFullResolver();
        var module = resolver.For(RoleFamily.Marketing);

        Assert.IsType<GenericScoringModule>(module);
    }


    [Fact]
    public void Other_MapsToGeneric_Module()
    {
        var resolver = MakeFullResolver();
        var module = resolver.For(RoleFamily.Other);

        Assert.IsType<GenericScoringModule>(module);
    }


    [Fact]
    public void Product_Alias_Resolves_Same_As_ProductManagement()
    {
        var resolver = MakeFullResolver();

        Assert.Same(
            resolver.For(RoleFamily.Product),
            resolver.For(RoleFamily.ProductManagement));
    }


    [Fact]
    public void Generic_Alias_Resolves_Same_As_Other()
    {
        var resolver = MakeFullResolver();

        Assert.Same(
            resolver.For(RoleFamily.Generic),
            resolver.For(RoleFamily.Other));
    }


    [Fact]
    public void Unknown_Family_Falls_Back_To_Generic_When_Available()
    {
        var resolver = MakeFullResolver();
        var module = resolver.For((RoleFamily)99);

        Assert.IsType<GenericScoringModule>(module);
    }
}
