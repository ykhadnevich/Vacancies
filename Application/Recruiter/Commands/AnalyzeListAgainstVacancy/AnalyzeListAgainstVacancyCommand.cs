using Application.Common.Auditing;
using Application.Common.Authorization;
using MediatR;

namespace Application.Recruiter.Commands.AnalyzeListAgainstVacancy;

public sealed record AnalyzeListAgainstVacancyCommand(
    Guid VacancyId,
    Guid CandidateListId)
    : IRequest<AnalyzeListAgainstVacancyResult>,
      IRequireRecruiterRole,
      IRequireVacancyOwnership,
      IRequireCandidateListOwnership,
      IAuditableRequest,
      IAuditableEntity
{
    public string AuditAction     => "AnalyzeListAgainstVacancy";
    public string AuditEntityType => "Vacancy";
    public Guid   AuditEntityId   => VacancyId;
}

public enum AnalyzeStatus
{
    Completed,
    AlreadyRunning,
    VacancyNotNormalized,
    NothingToScore
}

public sealed record AnalyzeListAgainstVacancyResult(
    AnalyzeStatus Status,
    int NewlyScored,
    int AlreadyScored,
    int Skipped,
    int Failed,
    string ScoringVersion);
