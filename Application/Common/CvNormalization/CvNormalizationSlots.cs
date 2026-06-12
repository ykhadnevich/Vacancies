namespace Application.Common.CvNormalization;


public sealed record CvNormalizationSlots(
    string SeniorityBands,
    string EducationRelevanceGuide,
    string TargetRolesGuidance,
    string ExperienceTypeNotes,
    string CanonicalizationExamples,
    string FullWorkedExample,
    string SkillBucketingNotes = "");
