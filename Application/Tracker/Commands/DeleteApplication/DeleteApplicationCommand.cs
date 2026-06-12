using Application.Common.Auditing;
using MediatR;

namespace Application.Tracker.Commands.DeleteApplication;

public record DeleteApplicationCommand(Guid ApplicationId, Guid UserId)
    : IRequest<bool>, IAuditableRequest, IAuditableEntity
{
    public string AuditAction     => "DeleteApplication";
    public string AuditEntityType => "Application";
    public Guid   AuditEntityId   => ApplicationId;
}
