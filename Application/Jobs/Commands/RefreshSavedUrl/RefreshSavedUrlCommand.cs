using Application.Common.Auditing;
using MediatR;

namespace Application.Jobs.Commands.RefreshSavedUrl;

public record RefreshSavedUrlCommand(Guid SavedUrlId)
    : IRequest<RefreshSavedUrlResult>, IAuditableRequest, IAuditableEntity
{
    public string AuditAction     => "RefreshSavedUrl";
    public string AuditEntityType => "SavedUrl";
    public Guid   AuditEntityId   => SavedUrlId;
}

public class RefreshSavedUrlResult
{
    public bool Success { get; init; }
    public int ParsedCount { get; init; }
    public int AddedCount { get; init; }
    public string? ErrorMessage { get; init; }
}
