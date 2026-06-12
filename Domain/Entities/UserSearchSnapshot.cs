namespace Domain.Entities;

/// <summary>
/// Server-side snapshot of the last v6 search result for a given (user, query) pair.
/// Lets the candidate-side UI show yesterday's analysis instantly on open without
/// paying for the full Mono pipeline again — fresh results are only generated on
/// the user's explicit "Refresh" action.
///
/// Uniqueness: <c>(UserId, QueryHash)</c>. Re-running the same search overwrites
/// the existing row via the repository's upsert.
/// </summary>
public sealed class UserSearchSnapshot
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    /// <summary>SHA-256 over the canonical (lower-cased, ordered) query parameters.</summary>
    public string QueryHash { get; private set; } = string.Empty;

    /// <summary>Free-text keywords stored separately for diagnostics / admin tooling.</summary>
    public string Keywords { get; private set; } = string.Empty;

    /// <summary>Full serialised <c>GetAggregatedJobsV6Result</c> as JSON. Stored as jsonb.</summary>
    public string ResponseJson { get; private set; } = string.Empty;

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
            Id           = Guid.NewGuid(),
            UserId       = userId,
            QueryHash    = queryHash,
            Keywords     = keywords ?? string.Empty,
            ResponseJson = responseJson,
            ExecutedAt   = DateTime.UtcNow,
        };
    }

    public void Replace(string responseJson)
    {
        if (string.IsNullOrWhiteSpace(responseJson))
            throw new ArgumentException("ResponseJson cannot be empty", nameof(responseJson));
        ResponseJson = responseJson;
        ExecutedAt   = DateTime.UtcNow;
    }
}
