using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Infrastructure.MlApi.Dtos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MlApi;


public sealed class MlApiFactualityService : IFactualityCheckService
{
    private const string Path = "v1/factuality/check";

    private readonly HttpClient _httpClient;
    private readonly ILogger<MlApiFactualityService> _logger;

    public MlApiFactualityService(
        HttpClient httpClient,
        ILogger<MlApiFactualityService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<FactualityVerdict>> CheckAsync(
        string document,
        IReadOnlyList<string> claims,
        CancellationToken ct = default)
    {
        if (claims.Count == 0) return Array.Empty<FactualityVerdict>();

        var request = new FactualityCheckRequestDto(document, claims);

        try
        {
            var resp = await _httpClient.PostAsJsonAsync(Path, request, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Factuality check failed: HTTP {Status}. Body: {Body}",
                    (int)resp.StatusCode,
                    body);


                return claims.Select(c => new FactualityVerdict(c, false, 0.0)).ToList();
            }

            var payload = await resp.Content.ReadFromJsonAsync<FactualityCheckResponseDto>(
                cancellationToken: ct);

            if (payload?.Results is null || payload.Results.Count == 0)
            {
                _logger.LogWarning(
                    "Factuality check returned empty results for {Count} claims",
                    claims.Count);
                return claims.Select(c => new FactualityVerdict(c, false, 0.0)).ToList();
            }

            return payload.Results
                .Select(r => new FactualityVerdict(
                    Claim: r.Claim,
                    IsSupported: r.Label == 1,
                    Confidence: r.Score))
                .ToList();
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogError(
                ex,
                "MLService factuality check threw — returning all-unsupported fallback for {Count} claims",
                claims.Count);
            return claims.Select(c => new FactualityVerdict(c, false, 0.0)).ToList();
        }
    }
}
