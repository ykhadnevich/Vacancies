using System.Text.Json;
using Infrastructure.RelevancePipeline.V2.Scoring;

namespace Infrastructure.Tests.RelevancePipeline.V2.Scoring;


public class AntiFlagEvaluatorTests
{
    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();


    [Fact]
    public void No_anti_requirements_returns_no_penalty()
    {
        var cv = Parse("{}");
        var vacancy = Parse("""{"anti_requirements":[]}""");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(1.0, result.Penalty);
        Assert.Empty(result.Triggered);
    }


    [Fact]
    public void Vacancy_without_anti_requirements_property_returns_no_penalty()
    {

        var cv = Parse("{}");
        var vacancy = Parse("{}");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(1.0, result.Penalty);
        Assert.Empty(result.Triggered);
    }


    [Fact]
    public void Foreign_language_flag_triggers_when_cv_lacks_language()
    {
        var cv = Parse("""{"languages":[{"language":"English","level":"B2"}]}""");
        var vacancy = Parse("""{"anti_requirements":["fluent French required"]}""");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(0.5, result.Penalty);
        Assert.Single(result.Triggered);
    }


    [Fact]
    public void Foreign_language_flag_does_not_trigger_when_cv_has_language()
    {
        var cv = Parse("""{"languages":[{"language":"French","level":"C1"}]}""");
        var vacancy = Parse("""{"anti_requirements":["fluent French required"]}""");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(1.0, result.Penalty);
        Assert.Empty(result.Triggered);
    }


    [Theory]
    [InlineData("contract-only role")]
    [InlineData("volunteer position")]
    [InlineData("unpaid internship")]
    public void Known_employment_flags_trigger(string flag)
    {
        var cv = Parse("{}");
        var vacancy = Parse($$"""{"anti_requirements":["{{flag}}"]}""");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(0.5, result.Penalty);
        Assert.Single(result.Triggered);
    }


    [Theory]
    [InlineData("onsite only Kyiv")]
    [InlineData("must be in Berlin")]
    [InlineData("based in Warsaw")]
    public void Known_location_flags_trigger(string flag)
    {
        var cv = Parse("{}");
        var vacancy = Parse($$"""{"anti_requirements":["{{flag}}"]}""");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(0.5, result.Penalty);
        Assert.Single(result.Triggered);
    }


    [Fact]
    public void Unknown_flag_does_NOT_trigger_penalty()
    {

        var cv = Parse("{}");
        var vacancy = Parse("""{"anti_requirements":["prefers candidates with sports background"]}""");

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(1.0, result.Penalty);
        Assert.Empty(result.Triggered);
    }


    [Fact]
    public void Two_triggered_flags_apply_harsher_penalty()
    {
        var cv = Parse("{}");
        var vacancy = Parse("""
            {"anti_requirements":["contract-only","onsite only Berlin"]}
            """);

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(0.2, result.Penalty);
        Assert.Equal(2, result.Triggered.Count);
    }


    [Fact]
    public void Mixed_known_and_unknown_only_counts_known_for_penalty()
    {

        var cv = Parse("{}");
        var vacancy = Parse("""
            {"anti_requirements":["contract-only","prefers caffeine addiction"]}
            """);

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(0.5, result.Penalty);
        Assert.Single(result.Triggered);
    }


    [Fact]
    public void Empty_and_whitespace_flags_ignored()
    {
        var cv = Parse("{}");
        var vacancy = Parse("""
            {"anti_requirements":["","   ","contract-only"]}
            """);

        var result = AntiFlagEvaluator.Evaluate(cv, vacancy);

        Assert.Equal(0.5, result.Penalty);
        Assert.Single(result.Triggered);
    }
}
