namespace Application.Common.Interfaces;

public interface IEmbeddingService
{
    Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default);
    Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(IReadOnlyList<string> texts, CancellationToken ct = default);
}
