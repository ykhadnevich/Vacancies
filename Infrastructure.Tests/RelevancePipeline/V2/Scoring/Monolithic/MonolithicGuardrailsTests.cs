using System.Text.Json;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;

namespace Infrastructure.Tests.RelevancePipeline.V2.Scoring.Monolithic;


public class MonolithicGuardrailsTests
{
    private static JsonElement Parse(string json) =>
        JsonDocument.Parse(json).RootElement.Clone();


    private static SubScores HighSubs() =>
        new(SkillMatch: 0.85, SeniorityMatch: 0.85, ExperienceMatch: 0.85,
            LanguageMatch: 0.85, EducationMatch: 0.85, RoleIntentMatch: 0.85,
            DomainAlignment: 0.85);


    [Fact]
    public void Junior_CV_on_Senior_vacancy_caps_seniority()
    {
        var cv = Parse("""{"seniority":"junior"}""");
        var vacancy = Parse("""{"seniority_required":"senior"}""");

        var (capped, report) = MonolithicGuardrails.Apply(HighSubs(), cv, vacancy);

        Assert.True(report.UnderQualifiedTriggered);
        Assert.Equal(MonolithicGuardrails.UnderQualifiedSeniorityCap, capped.SeniorityMatch);
        Assert.Contains("under_qualified", report.Reason);
    }


    [Fact]
    public void Middle_CV_on_Senior_vacancy_does_NOT_trigger()
    {

        var cv = Parse("""{"seniority":"middle"}""");
        var vacancy = Parse("""{"seniority_required":"senior"}""");

        var (capped, report) = MonolithicGuardrails.Apply(HighSubs(), cv, vacancy);

        Assert.False(report.UnderQualifiedTriggered);
        Assert.Equal(0.85, capped.SeniorityMatch);
    }


    [Fact]
    public void Cross_stack_guardrail_is_disabled_by_default()
    {

        var cv = Parse("""{"seniority":"middle","target_roles":["Mid Frontend Developer"]}""");
        var vacancy = Parse("""{"role_title":{"en":"Senior Backend Developer"}}""");

        var (capped, report) = MonolithicGuardrails.Apply(HighSubs(), cv, vacancy);


        Assert.False(report.CrossStackTriggered);
        Assert.Equal(0.85, capped.SkillMatch);
        Assert.Equal(0.85, capped.RoleIntentMatch);
    }


    [Fact]
    public void IsCrossStackHard_detector_still_works_when_called_directly()
    {

        var cv = Parse("""{"target_roles":["Mid Frontend Developer"]}""");
        var vacancy = Parse("""{"role_title":{"en":"Senior Backend Developer"}}""");

        Assert.True(MonolithicGuardrails.IsCrossStackHard(cv, vacancy, vacancyRawText: null, out var reason));
        Assert.Contains("cross_stack", reason);
    }


    [Fact]
    public void Unknown_seniority_does_NOT_trigger()
    {
        var cv = Parse("""{}""");
        var vacancy = Parse("""{}""");

        var (capped, report) = MonolithicGuardrails.Apply(HighSubs(), cv, vacancy);

        Assert.False(report.UnderQualifiedTriggered);
        Assert.False(report.CrossStackTriggered);
    }


    [Fact]
    public void Vacancy_seniority_can_be_inferred_from_raw_text()
    {

        var cv = Parse("""{"seniority":"junior"}""");
        var vacancy = Parse("""{}""");

        var (_, report) = MonolithicGuardrails.Apply(
            HighSubs(), cv, vacancy,
            vacancyRawText: "Senior Backend Developer wanted to lead our team.");

        Assert.True(report.UnderQualifiedTriggered);
    }


    [Fact]
    public void Vacancy_stack_can_be_inferred_from_raw_text()
    {

        var cv = Parse("""{"target_roles":["Mid Frontend Developer"]}""");
        var vacancy = Parse("""{}""");


        Assert.True(MonolithicGuardrails.IsCrossStackHard(cv, vacancy,
            vacancyRawText: "DevOps Engineer for AWS infrastructure.",
            out _));
    }


    [Fact]
    public void Guardrails_do_not_inflate_already_low_scores()
    {

        var lowSubs = new SubScores(
            SkillMatch: 0.05, SeniorityMatch: 0.05, ExperienceMatch: 0.05,
            LanguageMatch: 0.05, EducationMatch: 0.05, RoleIntentMatch: 0.05,
            DomainAlignment: 0.05);
        var cv = Parse("""{"seniority":"junior","target_roles":["Frontend Developer"]}""");
        var vacancy = Parse("""{"seniority_required":"senior","role_title":{"en":"Backend Engineer"}}""");

        var (capped, _) = MonolithicGuardrails.Apply(lowSubs, cv, vacancy);

        Assert.Equal(0.05, capped.SkillMatch);
        Assert.Equal(0.05, capped.SeniorityMatch);
    }


    [Fact]
    public void Detector_handles_ukrainian_titles()
    {

        var cv = Parse("""{"target_roles":["Розробник Frontend"]}""");
        var vacancy = Parse("""{"role_title":{"en":"DevOps Engineer"}}""");


        Assert.True(MonolithicGuardrails.IsCrossStackHard(cv, vacancy, null, out _));
    }
}
