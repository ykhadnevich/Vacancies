namespace Application.Common.Auditing;

public interface IAuditableRequest
{
    string AuditAction { get; }
}
