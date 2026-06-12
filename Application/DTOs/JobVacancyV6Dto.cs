using Domain.Enums;

namespace Application.DTOs;


public sealed record JobVacancyV6Dto(
    Guid Id,
    string Title,
    string Company,
    string? Location,
    string? Description,
    JobSource Source,
    WorkFormat WorkFormat,
    SeniorityLevel SeniorityLevel,
    string? Category,
    IReadOnlyList<string> Urls,
    DateTime PublishedAt,


    double Score,

    string Verdict,

    string ReasonEn,

    string ReasonUk,


    IReadOnlyDictionary<string, double> SubScores,

    double AntiFlagPenalty,


    IReadOnlyList<string> MatchedSkills,

    IReadOnlyList<string> MissingMustHaves,

    IReadOnlyList<string> TriggeredAntiFlags,


    string PipelineVersion,


    string? StrengthsEn = null,

    string? StrengthsUk = null,

    string? GapsEn = null,

    string? GapsUk = null,

    string? RecommendationEn = null,

    string? RecommendationUk = null);


public sealed record GetAggregatedJobsV6Result(
    IReadOnlyList<JobVacancyV6Dto> Jobs,
    int TotalReturned,
    int TotalAvailable,
    int SkippedNoAnalysis,
    string PipelineVersion);
