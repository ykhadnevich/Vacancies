using Application.Common.Auditing;
using MediatR;

namespace Application.User.Commands.DeleteCurrentUser;


public record DeleteCurrentUserCommand(Guid UserId)
    : IRequest<bool>, IAuditableRequest, IAuditableEntity
{
    // Account deletion is the single most security-sensitive mutation in the
    // system: it is the GDPR "right to erasure" operation and the natural
    // forensic question when a profile disappears. Always audited.
    public string AuditAction     => "DeleteCurrentUser";
    public string AuditEntityType => "UserProfile";
    public Guid   AuditEntityId   => UserId;
}
