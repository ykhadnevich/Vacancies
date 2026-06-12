using System.Text.Json;
using Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;

namespace Infrastructure.Tests.RelevancePipeline.V2.Scoring;


public class RoleIntentMatchCalculatorTests
{
    private readonly RoleIntentMatchCalculator _calc = new();

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();


    [Fact]
    public void Empty_role_title_returns_neutral_0_5()
    {

        var cv = Parse("""{"target_roles":["Senior Backend Developer"]}""");
        var vacancy = Parse("""{"role_title":{"en":""}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.5, score);
    }


    [Fact]
    public void Full_target_role_match_returns_1_0()
    {
        var cv = Parse("""{"target_roles":["Backend Developer"]}""");
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }


    [Fact]
    public void No_overlap_no_experience_returns_0()
    {

        var cv = Parse("""{"target_roles":["Product Manager"]}""");
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void Empty_target_roles_falls_back_to_experience_titles()
    {

        var cv = Parse("""
            {
              "target_roles":[],
              "experience":[
                {"role":"Backend Developer", "duration_months": 24}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);


        Assert.Equal(0.7, score, 3);
    }


    [Fact]
    public void Missing_target_roles_falls_back_to_experience()
    {

        var cv = Parse("""
            {
              "experience":[
                {"role":"Product Manager", "duration_months": 36}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Product Manager"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.7, score, 3);
    }


    [Fact]
    public void Target_roles_match_preferred_over_experience()
    {

        var cv = Parse("""
            {
              "target_roles":["Backend Developer"],
              "experience":[
                {"role":"Product Manager"}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }


    [Fact]
    public void Completely_empty_cv_returns_0()
    {

        var cv = Parse("""{"target_roles":[]}""");
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void Seniority_tokens_ignored_in_match()
    {

        var cv = Parse("""{"target_roles":["Junior Backend"]}""");
        var vacancy = Parse("""{"role_title":{"en":"Senior Backend"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }


    [Fact]
    public void Experience_with_title_field_also_works()
    {

        var cv = Parse("""
            {
              "experience":[
                {"title":"Frontend Developer", "duration_months": 12}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Frontend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.7, score, 3);
    }


    [Fact]
    public void Empty_role_title_dominates_even_when_target_roles_present()
    {

        var cv = Parse("""{"target_roles":["Backend Developer"]}""");
        var vacancy = Parse("""{}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.5, score);
    }


    [Fact]
    public void Multiple_experience_entries_uses_best_match()
    {

        var cv = Parse("""
            {
              "experience":[
                {"role":"Product Manager"},
                {"role":"Backend Developer"},
                {"role":"QA Engineer"}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);


        Assert.Equal(0.7, score, 3);
    }


    [Fact]
    public void Strong_fallback_beats_weak_primary()
    {

        var cv = Parse("""
            {
              "target_roles":["DevOps"],
              "experience":[
                {"role":"Backend Developer"}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);


        Assert.Equal(0.7, score, 3);
    }


    [Fact]
    public void Strong_primary_still_wins_over_perfect_fallback()
    {

        var cv = Parse("""
            {
              "target_roles":["Backend Developer"],
              "experience":[
                {"role":"Backend Developer"}
              ]
            }
            """);
        var vacancy = Parse("""{"role_title":{"en":"Backend Developer"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }
}
