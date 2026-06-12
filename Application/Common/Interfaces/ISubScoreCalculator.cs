using System.Text.Json;

namespace Application.Common.Interfaces;


public interface ISubScoreCalculator
{

    SubScoreAxis Axis { get; }


    double Compute(JsonElement cvSummary, JsonElement vacancyAnalysis);
}


public enum SubScoreAxis
{
    SkillMatch,
    SeniorityMatch,
    ExperienceMatch,
    LanguageMatch,
    EducationMatch,
    RoleIntentMatch,
    DomainAlignment
}
