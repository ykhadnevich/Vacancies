using Application.Common.Auditing;
using Application.Common.Authorization;
using MediatR;

namespace Application.Recruiter.Commands.DeleteCandidateList;

public sealed record DeleteCandidateListCommand(Guid CandidateListId)
    : IRequest<Unit>, IRequireRecruiterRole, IRequireCandidateListOwnership,
      IAuditableRequest, IAuditableEntity
{
    public string AuditAction     => "DeleteCandidateList";
    public string AuditEntityType => "CandidateList";
    public Guid   AuditEntityId   => CandidateListId;
}
