using Application.Common.Auditing;
using Application.Common.Authorization;
using MediatR;

namespace Application.Recruiter.Commands.DeleteRecruiterCandidate;

public sealed record DeleteRecruiterCandidateCommand(Guid CandidateId)
    : IRequest<Unit>, IRequireRecruiterRole, IAuditableRequest, IAuditableEntity
{
    public string AuditAction     => "DeleteRecruiterCandidate";
    public string AuditEntityType => "RecruiterCandidate";
    public Guid   AuditEntityId   => CandidateId;
}
