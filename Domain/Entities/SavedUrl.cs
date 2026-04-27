namespace Domain.Entities;

public class SavedUrl
{
    public Guid Id { get; private set; }
    public string Url { get; private set; } = default!;
    public string? Alias { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? LastParsedAt { get; private set; }
    public int LastParsedCount { get; private set; }

    private SavedUrl() { }

    public static SavedUrl Create(string url, string? alias = null) => new()
    {
        Id        = Guid.NewGuid(),
        Url       = url,
        Alias     = alias,
        CreatedAt = DateTime.UtcNow,
    };

    public void RecordParsed(int count)
    {
        LastParsedAt    = DateTime.UtcNow;
        LastParsedCount = count;
    }
}
