namespace Domain.Entities;

public sealed class UserSearchSnapshot
{
    public const int CurrentSchemaVersion = 1;

    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string QueryHash { get; private set; } = string.Empty;
    public string Keywords { get; private set; } = string.Empty;
    public string ResponseJson { get; private set; } = string.Empty;
    public int SchemaVersion { get; private set; }
    public DateTime ExecutedAt { get; private set; }

    private UserSearchSnapshot() { }

    public static UserSearchSnapshot Create(
        Guid userId,
        string queryHash,
        string keywords,
        string responseJson)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (string.IsNullOrWhiteSpace(queryHash))
            throw new ArgumentException("QueryHash cannot be empty", nameof(queryHash));
        if (string.IsNullOrWhiteSpace(responseJson))
            throw new ArgumentException("ResponseJson cannot be empty", nameof(responseJson));

        return new UserSearchSnapshot
        {
            Id            = Guid.NewGuid(),
            UserId        = userId,
            QueryHash     = queryHash,
            Keywords      = keywords ?? string.Empty,
            ResponseJson  = responseJson,
            SchemaVersion = CurrentSchemaVersion,
            ExecutedAt    = DateTime.UtcNow,
        };
    }

    public void Replace(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
            throw new ArgumentException("ResponseJson cannot be empty", nameof(responseJson));
        ResponseJson  = responseJson;
        SchemaVersion = CurrentSchemaVersion;
        ExecutedAt    = DateTime.UtcNow;
    }

    public bool IsCurrentSchema() => SchemaVersion == CurrentSchemaVersion;
}
