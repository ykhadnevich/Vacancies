namespace Application.DTOs.Eval;


public sealed record EvalPairResultDto(
    string CvId,
    Guid VacancyId,
    string VacancyTitle,
    int Rank,
    double Score,
    string Verdict,
    double SkillMatch,
    double SeniorityMatch,
    double ExperienceMatch,
    double LanguageMatch,
    double EducationMatch,
    double RoleIntentMatch,
    double DomainAlignment,
    double AntiFlagPenalty,
    string ReasonEn,
    string? ReasonUk,
    IReadOnlyList<string> MatchedSkills,
    IReadOnlyList<string> MissingMustHaves,
    IReadOnlyList<string> TriggeredAntiFlags);
