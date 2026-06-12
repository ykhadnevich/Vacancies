using System.Net.Http.Json;
using System.Net.Sockets;
using Application.Common.Interfaces;
using Infrastructure.MlApi.Dtos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MlApi;


public class MlApiEmbeddingService : IEmbeddingService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MlApiEmbeddingService> _logger;

    public MlApiEmbeddingService(HttpClient httpClient, ILogger<MlApiEmbeddingService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<float[]> GetEmbeddingAsync(string text, CancellationToken ct = default)
    {
        var results = await GetEmbeddingsBatchAsync([text], ct);
        return results.First();
    }

    public async Task<IReadOnlyList<float[]>> GetEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        try
        {

            var request = new EmbedRequestDto(texts.ToList(), "cv");

            var response = await _httpClient.PostAsJsonAsync("/v1/embed/cv", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponseDto>(
                cancellationToken: ct);

            return result!.Embeddings
                .Select(e => e.ToArray())
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException
                                    || ex.InnerException is SocketException)
        {
            _logger.LogWarning("ML API unavailable (localhost:8000) — CV embeddings skipped.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ML API embedding failed for {Count} texts", texts.Count);
            throw;
        }
    }

    public async Task<IReadOnlyList<float[]>> GetVacancyEmbeddingsBatchAsync(
        IReadOnlyList<string> texts,
        CancellationToken ct = default)
    {
        try
        {
            var request = new EmbedRequestDto(texts.ToList(), "vacancy");

            var response = await _httpClient.PostAsJsonAsync("/v1/embed/vacancy", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<EmbedResponseDto>(
                cancellationToken: ct);

            return result!.Embeddings
                .Select(e => e.ToArray())
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or SocketException
                                    || ex.InnerException is SocketException)
        {
            _logger.LogWarning("ML API unavailable (localhost:8000) — vacancy embeddings skipped.");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "ML API vacancy embedding failed for {Count} texts", texts.Count);
            throw;
        }
    }
}
