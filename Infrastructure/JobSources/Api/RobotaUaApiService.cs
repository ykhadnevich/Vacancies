using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.ValueObjects;
using Infrastructure.Helpers;

namespace Infrastructure.JobSources.Api;

public class RobotaUaApiService : IJobSourceService
{
    private readonly HttpClient _httpClient;
    private const string BaseUrl = "https://api.robota.ua";

    public string SourceName => "robota.ua";

    public RobotaUaApiService(HttpClient httpClient, IJobDescriptionFetcher descriptionFetcher)
    {
        _httpClient = httpClient;
    }

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        CancellationToken ct = default)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = cts.Token;

        var url = $"{BaseUrl}/vacancy/search?keyWords={Uri.EscapeDataString(keywords)}";
        var response = await _httpClient.GetAsync(url, token);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(token);
        var data = JsonSerializer.Deserialize<RobotaUaResponse>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        var vacancies = data?.Documents ?? new List<RobotaUaVacancy>();
        var jobs = await Task.WhenAll(vacancies.Select(v => MapWithDescriptionAsync(v, token)));
        return jobs.Where(j => j != null).Cast<JobVacancy>().ToList();
    }

    private async Task<JobVacancy?> MapWithDescriptionAsync(RobotaUaVacancy v, CancellationToken token)
    {
        var salaryRaw = v.Salary?.ValueKind switch
        {
            JsonValueKind.String => v.Salary.Value.GetString(),
            JsonValueKind.Number => v.Salary.Value.GetRawText(),
            _ => null
        };

        var vacancyUrl = $"https://robota.ua/company{v.NotebookId}/vacancy{v.Id}";

        var rawName = v.Name ?? string.Empty;
        string title;
        string company;

        if (!string.IsNullOrEmpty(v.CompanyName))
        {
            title = rawName;
            company = v.CompanyName;
        }
        else if (rawName.Contains(" в "))
        {
            var parts = rawName.Split(" в ", 2);
            title = parts[0].Trim();
            company = parts[1].Split(',')[0].Trim();
        }
        else
        {
            title = rawName;
            company = string.Empty;
        }

        string? description = HtmlHelper.StripHtml(v.ShortDescription);

        try
        {
            var detailResponse = await _httpClient.GetAsync($"{BaseUrl}/vacancy?id={v.Id}", token);
            if (detailResponse.IsSuccessStatusCode)
            {
                var detailJson = await detailResponse.Content.ReadAsStringAsync(token);
                var detail = JsonSerializer.Deserialize<RobotaUaDetail>(detailJson,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (!string.IsNullOrEmpty(detail?.Description))
                {
                    var cleaned = HtmlHelper.StripHtml(detail.Description);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                        description = cleaned;
                }
                else if (!string.IsNullOrEmpty(detail?.ShortDescription))
                {
                    var cleaned = HtmlHelper.StripHtml(detail.ShortDescription);
                    if (!string.IsNullOrWhiteSpace(cleaned))
                        description = cleaned;
                }
            }
        }
        catch { }

        return JobVacancy.Create(
            title: title,
            company: company,
            url: vacancyUrl,
            source: JobSource.RobotaUa,
            publishedAt: v.PublishedAt ?? DateTime.UtcNow,
            description: description,
            location: v.CityName,
            salary: salaryRaw != null ? new Salary(salaryRaw) : null
        );
    }

    private record RobotaUaDetail(string? Description, string? ShortDescription);
    private record RobotaUaResponse(List<RobotaUaVacancy>? Documents);
    private record RobotaUaVacancy(
        long Id,
        long NotebookId,
        string? Name,
        string? CompanyName,
        string? CityName,
        string? ShortDescription,
        JsonElement? Salary,
        DateTime? PublishedAt);
}
