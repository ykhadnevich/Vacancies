using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Infrastructure.RelevancePipeline.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline;

public class GeminiScoringService : IGeminiScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiScoringService> _logger;

    private const string Model = "gemini-2.5-flash-lite";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiScoringService(HttpClient httpClient, IConfiguration config, ILogger<GeminiScoringService> logger)
    {
        _httpClient = httpClient;
        _apiKey = config["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeminiJobScore>> ScoreJobsAsync(
        IReadOnlyList<(Guid Id, string Title, string Company, string? Description)> jobs,
        string userProfileText,
        CancellationToken ct = default)
    {
        try
        {
            var tasks = jobs.Select(job => ScoreSingleJobAsync(job, userProfileText, ct));
            var results = await Task.WhenAll(tasks);

            var totalInput  = results.Sum(r => r.InputTokens);
            var totalOutput = results.Sum(r => r.OutputTokens);
            _logger.LogInformation(
                "Gemini batch complete — jobs: {Count} | total input tokens: {In} | total output tokens: {Out} | total tokens: {Total}",
                results.Length, totalInput, totalOutput, totalInput + totalOutput);

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini batch scoring failed: {Message}", ex.Message);
            return jobs.Select(j => new GeminiJobScore(j.Id, 50f, "Scoring unavailable")).ToList();
        }
    }

    private async Task<GeminiJobScore> ScoreSingleJobAsync(
        (Guid Id, string Title, string Company, string? Description) job,
        string userProfileText,
        CancellationToken ct)
    {
        try
        {
            var desc = job.Description?[..Math.Min(800, job.Description?.Length ?? 0)] ?? "немає опису";
            var prompt = ScoringPrompt.Build(userProfileText, job.Title, job.Company, desc);

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature = 0.1,
                    maxOutputTokens = 100
                }
            };

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            var result = ParseResponse(json, job.Id);

            _logger.LogInformation(
                "Gemini [{Title}] — input: {In} tokens | output: {Out} tokens | score: {Score}",
                job.Title, result.InputTokens, result.OutputTokens, result.Score);

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini scoring failed for [{Title}]: {Message}", job.Title, ex.Message);
            return new GeminiJobScore(job.Id, 50f, "Scoring unavailable");
        }
    }

    private static GeminiJobScore ParseResponse(string json, Guid jobId)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            var text = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString() ?? "{}";

            text = text.Replace("```json", "").Replace("```", "").Trim();

            using var resultDoc = JsonDocument.Parse(text);
            var score  = resultDoc.RootElement.GetProperty("score").GetSingle();
            var reason = resultDoc.RootElement.GetProperty("reason").GetString() ?? "";

            // Extract token usage from usageMetadata
            var inputTokens  = 0;
            var outputTokens = 0;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount",     out var p)) inputTokens  = p.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var c)) outputTokens = c.GetInt32();
            }

            return new GeminiJobScore(jobId, Math.Clamp(score, 0, 100), reason, inputTokens, outputTokens);
        }
        catch
        {
            return new GeminiJobScore(jobId, 50f, "Unable to parse score");
        }
    }
}