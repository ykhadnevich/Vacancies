using Application.Common.Auditing;
using MediatR;
using Domain.Enums;

namespace Application.Tracker.Commands.UpdateApplicationStatus;

public record UpdateApplicationStatusCommand : IRequest<bool>, IAuditableRequest, IAuditableEntity
{
    public Guid ApplicationId { get; init; }
    public ApplicationStatus? NewStatus { get; init; }
    public string? PipelineStep { get; init; }
    public bool? PipelineStepValue { get; init; }
    public string? Notes { get; init; }
    public bool UpdateNotes { get; init; } = false;

    public string AuditAction     => "UpdateApplicationStatus";
    public string AuditEntityType => "Application";
    public Guid   AuditEntityId   => ApplicationId;
}
