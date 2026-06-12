namespace Domain.Entities;


public sealed class GeminiCostLogEntry
{
    public Guid Id { get; private set; }


    public DateTime Timestamp { get; private set; }


    public Guid? UserId { get; private set; }


    public Guid RequestId { get; private set; }


    public string RequestKind { get; private set; } = string.Empty;


    public string Stage { get; private set; } = string.Empty;

    public int Calls { get; private set; }
    public double DurationMs { get; private set; }
    public long InputTokens { get; private set; }
    public long OutputTokens { get; private set; }
    public double CostUsd { get; private set; }


    public string? Keywords { get; private set; }

    private GeminiCostLogEntry() { }

    public static GeminiCostLogEntry Create(
        Guid requestId,
        string requestKind,
        string stage,
        int calls,
        double durationMs,
        long inputTokens,
        long outputTokens,
        double costUsd,
        Guid? userId = null,
        string? keywords = null) =>
        new()
        {
            Id           = Guid.NewGuid(),
            Timestamp    = DateTime.UtcNow,
            UserId       = userId,
            RequestId    = requestId,
            RequestKind  = requestKind,
            Stage        = stage,
            Calls        = calls,
            DurationMs   = durationMs,
            InputTokens  = inputTokens,
            OutputTokens = outputTokens,
            CostUsd      = costUsd,
            Keywords     = keywords?.ToLowerInvariant().Trim(),
        };
}
