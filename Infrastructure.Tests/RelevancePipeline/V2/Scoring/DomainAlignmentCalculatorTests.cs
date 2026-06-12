using System.Text.Json;
using Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;

namespace Infrastructure.Tests.RelevancePipeline.V2.Scoring;


public class DomainAlignmentCalculatorTests
{
    private readonly DomainAlignmentCalculator _calc = new();

    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();


    private static JsonElement BuildCv(string targetRole, params string[] domainSkills)
    {
        var skillsJson = string.Join(",", domainSkills.Select(s => $"\"{s}\""));
        var json =
            "{" +
            $"\"target_roles\":[\"{targetRole}\"]," +
            $"\"domain_skills\":[{skillsJson}]" +
            "}";
        return Parse(json);
    }

    private static JsonElement BuildVacancy(string domainEn)
    {
        var json = "{\"domain_context\":{\"en\":\"" + domainEn + "\"}}";
        return Parse(json);
    }


    [Fact]
    public void Tech_role_with_zero_overlap_returns_zero()
    {

        var cv = BuildCv("Senior Backend Developer", "fintech", "banking");
        var vacancy = BuildVacancy("healthcare medical");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void PM_role_with_zero_overlap_returns_soft_floor_0_3()
    {

        var cv = BuildCv("Senior Product Manager", "fintech");
        var vacancy = BuildVacancy("healthcare medical");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.3, score);
    }


    [Fact]
    public void Designer_role_with_zero_overlap_returns_soft_floor_0_3()
    {
        var cv = BuildCv("Senior UX Designer", "fintech");
        var vacancy = BuildVacancy("healthcare medical");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.3, score);
    }


    [Fact]
    public void Other_role_with_zero_overlap_returns_default_floor_0_5()
    {

        var cv = BuildCv("QA Engineer", "fintech");
        var vacancy = BuildVacancy("healthcare medical");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.5, score);
    }


    [Fact]
    public void Full_overlap_for_tech_returns_1_0()
    {
        var cv = BuildCv("Backend Developer", "fintech", "banking");
        var vacancy = BuildVacancy("fintech");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }


    [Fact]
    public void Full_overlap_for_PM_returns_1_0()
    {
        var cv = BuildCv("Product Manager", "fintech", "banking");
        var vacancy = BuildVacancy("fintech");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }


    [Fact]
    public void Empty_domain_for_tech_returns_0()
    {

        var cv = BuildCv("Backend Developer", "fintech");
        var vacancy = BuildVacancy("");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void Empty_domain_for_PM_returns_0_5_neutral()
    {

        var cv = BuildCv("Product Manager", "fintech");
        var vacancy = BuildVacancy("");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.5, score);
    }


    [Fact]
    public void Other_domain_treated_as_empty()
    {

        var cv = BuildCv("Backend Developer", "fintech");
        var vacancy = BuildVacancy("other");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void Partial_overlap_tech_pure_jaccard()
    {

        var cv = BuildCv("Backend Developer", "fintech");
        var vacancy = BuildVacancy("fintech banking saas");

        var score = _calc.Compute(cv, vacancy);

        Assert.InRange(score, 0.32, 0.34);
    }


    [Fact]
    public void Partial_overlap_PM_floor_lifts_above_jaccard()
    {

        var cv = BuildCv("Product Manager", "fintech");
        var vacancy = BuildVacancy("fintech banking saas");

        var score = _calc.Compute(cv, vacancy);

        Assert.InRange(score, 0.53, 0.54);
    }


    [Fact]
    public void Missing_domain_context_property_returns_empty_floor()
    {

        var cv = Parse("""{"target_roles":["Backend Developer"],"domain_skills":["fintech"]}""");
        var vacancy = Parse("""{}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void Missing_domain_skills_property_returns_zero_jaccard_for_tech()
    {

        var cv = Parse("""{"target_roles":["Backend Developer"]}""");
        var vacancy = Parse("""{"domain_context":{"en":"fintech"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.0, score);
    }


    [Fact]
    public void Missing_domain_skills_property_returns_soft_floor_for_PM()
    {

        var cv = Parse("""{"target_roles":["Product Manager"]}""");
        var vacancy = Parse("""{"domain_context":{"en":"fintech"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(0.3, score);
    }


    [Fact]
    public void Domain_skills_with_non_string_entries_ignored()
    {

        var cv = Parse("""
            {
              "target_roles":["Backend Developer"],
              "domain_skills":["fintech", 42, null]
            }
            """);
        var vacancy = Parse("""{"domain_context":{"en":"fintech"}}""");

        var score = _calc.Compute(cv, vacancy);

        Assert.Equal(1.0, score);
    }
}
