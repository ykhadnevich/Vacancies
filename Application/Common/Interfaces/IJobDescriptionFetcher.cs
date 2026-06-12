namespace Application.Common.Interfaces;

public interface IJobDescriptionFetcher
{
    Task<string?> FetchDescriptionAsync(string url, CancellationToken ct = default);
}
