using System.Net.Http.Json;
using Application.Common.Interfaces;
using Infrastructure.MlApi.Dtos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MlApi;


public class MlApiReasoningService : IReasoningService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MlApiReasoningService> _logger;

    public MlApiReasoningService(HttpClient httpClient, ILogger<MlApiReasoningService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<ReasoningResult> GenerateReasonAsync(
        string cvText,
        string jobTitle,
        string jobDescription,
        float score,
        CancellationToken ct = default)
    {
        try
        {
            var request = new ReasonRequestDto(
                CvText: cvText[..Math.Min(2000, cvText.Length)],
                JobTitle: jobTitle,
                JobDescription: jobDescription[..Math.Min(800, jobDescription.Length)],
                Score: score);

            var response = await _httpClient.PostAsJsonAsync("/v1/reason", request, ct);
            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ReasonResponseDto>(
                cancellationToken: ct);

            _logger.LogDebug(
                "ML reason generated for [{JobTitle}] via {ModelVersion} in {Latency}ms",
                jobTitle, result!.ModelVersion, result.LatencyMs);

            return new ReasoningResult(result.Reason, result.ModelVersion);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ML API reason generation failed for [{JobTitle}]", jobTitle);
            return new ReasoningResult(string.Empty, string.Empty);
        }
    }
}
