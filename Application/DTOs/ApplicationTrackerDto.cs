using Domain.Enums;

namespace Application.DTOs;

public class ApplicationTrackerDto
{
    public Guid Id { get; init; }
    public Guid? JobVacancyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string? Location { get; init; }
    public string? Salary { get; init; }
    public string Url { get; init; } = string.Empty;
    public SeniorityLevel SeniorityLevel { get; init; }
    public ApplicationStatus Status { get; init; }
    public IReadOnlyDictionary<string, bool> PipelineSteps { get; init; }
        = new Dictionary<string, bool>();
    public string? Notes { get; init; }
    public DateTime AddedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsManuallyAdded { get; init; }


    public double?                            Score              { get; init; }
    public string?                            Verdict            { get; init; }
    public IReadOnlyList<string>?             MatchedSkills      { get; init; }
    public IReadOnlyList<string>?             MissingMustHaves   { get; init; }
    public IReadOnlyList<string>?             TriggeredAntiFlags { get; init; }
    public string?                            ReasonShort        { get; init; }
    public string?                            StrengthsEn        { get; init; }
    public string?                            StrengthsUk        { get; init; }
    public string?                            GapsEn             { get; init; }
    public string?                            GapsUk             { get; init; }
    public string?                            RecommendationEn   { get; init; }
    public string?                            RecommendationUk   { get; init; }
    public IReadOnlyDictionary<string, double>? SubScores        { get; init; }
    public string?                            CvFileName         { get; init; }
    public string?                            PipelineVersion    { get; init; }
    public DateTime?                          AnalyzedAt         { get; init; }
}
