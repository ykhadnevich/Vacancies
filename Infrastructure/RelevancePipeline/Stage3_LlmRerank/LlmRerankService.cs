using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.DTOs;

namespace Infrastructure.RelevancePipeline.Stage3_LlmRerank;

public class LlmRerankService : ILlmRerankService
{
    private readonly HttpClient _httpClient;

    public LlmRerankService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<RankedJobDto>> RerankAsync(
        IReadOnlyList<JobVacancyDto> jobs,
        string userProfileSummary,
        CancellationToken ct = default)
    {
        var jobsList = jobs.Select((j, i) =>
            $"{i + 1}. [{j.Id}] {j.Title} at {j.Company}\n" +
            $"   Skills: {(j.Description != null ? j.Description[..Math.Min(200, j.Description.Length)] : string.Empty)}...")
            .Aggregate((a, b) => $"{a}\n{b}");

        var prompt =
            $"You are a job matching expert. Analyze these job vacancies for a candidate.\n\n" +
            $"Candidate Profile:\n{userProfileSummary}\n\n" +
            $"Job Vacancies:\n{jobsList}\n\n" +
            "Return ONLY a JSON array with top 3 matches:\n" +
            "[{\"jobId\": \"guid\", \"rank\": 1, \"score\": 95.5, \"reasoning\": \"...\"}]";

        var body = new
        {
            model = "gpt-4o-mini",
            messages = new[] { new { role = "user", content = prompt } },
            response_format = new { type = "json_object" }
        };

        var response = await _httpClient.PostAsJsonAsync(
            "https://api.openai.com/v1/chat/completions", body, ct);

        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(ct);
        var data = JsonSerializer.Deserialize<LlmResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var content = data?.Choices?.FirstOrDefault()?.Message?.Content ?? "[]";
        return JsonSerializer.Deserialize<List<RankedJobDto>>(content,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
               ?? new List<RankedJobDto>();
    }

    private record LlmResponse(List<LlmChoice>? Choices);
    private record LlmChoice(LlmMessage? Message);
    private record LlmMessage(string? Content);
}
