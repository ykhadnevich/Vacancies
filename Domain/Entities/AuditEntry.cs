namespace Domain.Entities;

public sealed class AuditEntry
{
    public Guid Id { get; private set; }
    public Guid? UserId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public string? EntityType { get; private set; }
    public Guid? EntityId { get; private set; }
    public string? PayloadJson { get; private set; }
    public string Outcome { get; private set; } = "Success";
    public DateTime Timestamp { get; private set; }
    public string? IpAddress { get; private set; }
    public string? UserAgent { get; private set; }

    private AuditEntry() { }

    public static AuditEntry Create(
        string action,
        Guid? userId,
        string? entityType,
        Guid? entityId,
        string? payloadJson,
        string? ipAddress,
        string? userAgent,
        string outcome = "Success")
    {
        if (string.IsNullOrWhiteSpace(action))
            throw new ArgumentException("Action cannot be empty", nameof(action));

        return new AuditEntry
        {
            Id          = Guid.NewGuid(),
            UserId      = userId,
            Action      = action,
            EntityType  = string.IsNullOrWhiteSpace(entityType) ? null : entityType,
            EntityId    = entityId,
            PayloadJson = payloadJson,
            Outcome     = string.IsNullOrWhiteSpace(outcome) ? "Success" : outcome,
            Timestamp   = DateTime.UtcNow,
            IpAddress   = string.IsNullOrWhiteSpace(ipAddress) ? null : ipAddress,
            UserAgent   = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent,
        };
    }
}
