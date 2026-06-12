using Application.Common.Auditing;
using MediatR;
using Application.DTOs;
using Domain.Enums;

namespace Application.Tracker.Commands.AddToTracker;

public record AddToTrackerCommand : IRequest<ApplicationTrackerDto>, IAuditableRequest, IAuditablePayload
{
    public Guid?           JobVacancyId   { get; init; }
    public string?         Title          { get; init; }
    public string?         Company        { get; init; }
    public string?         Location       { get; init; }
    public string?         Url            { get; init; }
    public string?         Salary         { get; init; }
    public SeniorityLevel  SeniorityLevel { get; init; } = SeniorityLevel.NotSpecified;


    public double?                  Score              { get; init; }
    public string?                  Verdict            { get; init; }
    public List<string>?            MatchedSkills      { get; init; }
    public List<string>?            MissingMustHaves   { get; init; }
    public List<string>?            TriggeredAntiFlags { get; init; }
    public string?                  ReasonShort        { get; init; }
    public string?                  StrengthsEn        { get; init; }
    public string?                  StrengthsUk        { get; init; }
    public string?                  GapsEn             { get; init; }
    public string?                  GapsUk             { get; init; }
    public string?                  RecommendationEn   { get; init; }
    public string?                  RecommendationUk   { get; init; }
    public Dictionary<string,double>? SubScores        { get; init; }
    public string?                  PipelineVersion    { get; init; }

    public string AuditAction => "AddToTracker";

    // Strengths/Gaps/Recommendation/SubScores may leak CV content — record structural fields only.
    public IReadOnlyDictionary<string, object?>? BuildAuditPayload() => new Dictionary<string, object?>
    {
        ["jobVacancyId"]    = JobVacancyId,
        ["title"]           = Title,
        ["company"]         = Company,
        ["location"]        = Location,
        ["url"]             = Url,
        ["seniorityLevel"]  = SeniorityLevel.ToString(),
        ["score"]           = Score,
        ["verdict"]         = Verdict,
        ["pipelineVersion"] = PipelineVersion,
    };
}
