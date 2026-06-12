using System.Net.Http.Json;
using Application.Common.Interfaces;
using Infrastructure.MlApi.Dtos;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MlApi;


public class MlApiScoringService : IRelevanceScoringService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<MlApiScoringService> _logger;

    public MlApiScoringService(HttpClient httpClient, ILogger<MlApiScoringService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<IReadOnlyList<RelevanceScoreResult>> ScoreJobsAsync(
        IReadOnlyList<JobScoringInput> jobs,
        string userProfileText,
        CancellationToken ct = default)
    {
        try
        {
            var request = new ScoreRequestDto(
                UserProfileText: userProfileText,
                Jobs: jobs
                    .Select(j => new ScoreItemDto(j.Id, j.Title, j.Company, j.Description ?? string.Empty))
                    .ToList());

            _logger.LogInformation(
                "Calling ML API scoring: {Count} jobs, profile length: {Len} chars",
                jobs.Count, userProfileText.Length);

            var response = await _httpClient.PostAsJsonAsync("/v1/score/pairs", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "ML API scoring returned {Status}: {Body}",
                    (int)response.StatusCode, body[..Math.Min(500, body.Length)]);
            }

            response.EnsureSuccessStatusCode();

            var result = await response.Content.ReadFromJsonAsync<ScoreResponseDto>(
                cancellationToken: ct);

            _logger.LogInformation(
                "ML API scoring complete — {Count} jobs scored via {Method}",
                result!.Results.Count,
                result.Results.FirstOrDefault()?.Method ?? "unknown");

            return result.Results
                .Select(r => new RelevanceScoreResult(r.JobId, r.Score))
                .ToList();
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "ML API scoring HTTP error for {Count} jobs (status: {Status}), falling back to score=50",
                jobs.Count, ex.StatusCode);
            return jobs.Select(j => new RelevanceScoreResult(j.Id, 50f)).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "ML API scoring failed for {Count} jobs [{ExType}], falling back to score=50",
                jobs.Count, ex.GetType().Name);
            return jobs.Select(j => new RelevanceScoreResult(j.Id, 50f)).ToList();
        }
    }
}
