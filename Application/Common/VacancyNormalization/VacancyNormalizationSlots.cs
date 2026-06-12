namespace Application.Common.VacancyNormalization;


public sealed record VacancyNormalizationSlots(
    string SeniorityKeywords,
    string SkillCanonicalization,
    string MustVsNiceMarkers,
    string AntiRequirementsGuide,
    string FullWorkedExample,
    string SoftTraitFilterGuide = "");
