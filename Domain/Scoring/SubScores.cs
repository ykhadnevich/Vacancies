namespace Domain.Scoring;


public sealed record SubScores(
    double SkillMatch,
    double SeniorityMatch,
    double ExperienceMatch,
    double LanguageMatch,
    double EducationMatch,
    double RoleIntentMatch,
    double DomainAlignment);
