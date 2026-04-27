using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline.Stage2_Embedding;

public class EmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private const string Model = "text-embedding-3-small";

    public EmbeddingService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<float[]> GetEmbeddingAsync(
        string text, CancellationToken ct = default)
    {
        var results = await GetEmbeddingsBatchAsync(new[] { text }, ct);
        return results.First();
    }

    public async Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(
        IReadOnlyList<string> texts, CancellationToken ct = default)
    {
        var body = new { input = texts, model = Model };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/embeddings", body, ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<OpenAiEmbeddingResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        return data!.Data
            .OrderBy(d => d.Index)
            .Select(d => d.Embedding)
            .ToList();
    }

    private record OpenAiEmbeddingResponse(List<EmbeddingData> Data);
    private record EmbeddingData(int Index, float[] Embedding);
}
