using System.Net.Http.Json;
using Application.Common.Interfaces;
using Infrastructure.MlApi.Dtos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MlApi;


public class MlApiCvExtractionService : ICvExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MlApiCvExtractionService> _logger;

    public MlApiCvExtractionService(
        HttpClient httpClient,
        ILogger<MlApiCvExtractionService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<CvExtractionResult> ExtractAsync(
        string cvRawText,
        CancellationToken ct = default)
    {
        try
        {
            var request = new ExtractCvRequestDto(
                CvText: cvRawText[..Math.Min(4000, cvRawText.Length)]);

            var response = await _httpClient.PostAsJsonAsync("/v1/extract-cv", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ExtractCvResponseDto>(
                cancellationToken: ct);

            _logger.LogDebug(
                "CV extracted via {ModelVersion} in {Latency}ms",
                result!.ModelVersion, result.LatencyMs);

            return new CvExtractionResult(result.Summary, result.ModelVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML API CV extraction failed");
            return new CvExtractionResult(string.Empty, string.Empty);
        }
    }
}
