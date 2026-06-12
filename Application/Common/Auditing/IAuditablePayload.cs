namespace Application.Common.Auditing;

public interface IAuditablePayload
{
    IReadOnlyDictionary<string, object?>? BuildAuditPayload();
}
