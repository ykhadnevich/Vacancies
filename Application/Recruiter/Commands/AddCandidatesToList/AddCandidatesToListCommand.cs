using Application.Common.Auditing;
using Application.Common.Authorization;
using MediatR;

namespace Application.Recruiter.Commands.AddCandidatesToList;

public sealed record AddCandidatesToListCommand(
    Guid CandidateListId,
    IReadOnlyList<NewCandidateInput> Candidates)
    : IRequest<AddCandidatesToListResult>, IRequireRecruiterRole, IRequireCandidateListOwnership,
      IAuditableRequest, IAuditableEntity, IAuditablePayload
{
    public string AuditAction     => "AddCandidatesToList";
    public string AuditEntityType => "CandidateList";
    public Guid   AuditEntityId   => CandidateListId;

    // CvRawText is PII — record structural metadata only.
    public IReadOnlyDictionary<string, object?>? BuildAuditPayload() => new Dictionary<string, object?>
    {
        ["candidateListId"]    = CandidateListId,
        ["candidateCount"]     = Candidates?.Count ?? 0,
        ["candidateNames"]     = Candidates?.Select(c => c.CandidateName).Where(n => !string.IsNullOrWhiteSpace(n)).ToArray(),
        ["totalCvTextSize"]    = Candidates?.Sum(c => c.CvRawText?.Length ?? 0) ?? 0,
    };
}

public sealed record NewCandidateInput(string CvRawText, string? CandidateName = null);

public sealed record AddCandidatesToListResult(
    int Normalized,
    int Failed,
    IReadOnlyList<AddedCandidateSummary> Items);

public sealed record AddedCandidateSummary(
    Guid CandidateId,
    string? CandidateName,
    bool Normalized,
    string? Error);
