namespace Application.Common.Auditing;

public interface IAuditableEntity
{
    string AuditEntityType { get; }
    Guid AuditEntityId { get; }
}
