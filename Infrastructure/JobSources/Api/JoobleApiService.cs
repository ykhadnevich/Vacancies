using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Domain.ValueObjects;
using Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.JobSources.Api;

/// <summary>
/// Official Jooble Search API client.
///
///   POST https://jooble.org/api/{apiKey}
///   Content-Type: application/json
///   { "keywords": "...", "location": "Ukraine", "page": 1, "ResultOnPage": 20 }
///
/// Docs: https://jooble.org/api/about
///
/// Notes from manual testing (2026-06):
///   • The endpoint MUST be https://jooble.org/api/  — the friend-circulated
///     http://ua.jooble.org/api/ form returns {"errorCode":4,"Access denied"}.
///   • `location` must be in English ("Ukraine"). Cyrillic "Україна" returns 0 jobs.
///   • Free tier is 500 requests / day per key.
/// </summary>
public class JoobleApiService : IJobSourceService
{
    private const string ApiBaseUrl = "https://jooble.org/api/";
    private const int ResultsPerPage = 20;
    private const int MaxPages = 3; // 3×20 = 60 results per keyword
    // No DefaultLocation: Jooble's `location` filter is a strict country-match
    // and most UA listings on jooble.org don't have country=UA properly tagged,
    // so passing "Ukraine" returns ~2 results vs ~60k with no location.

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    private readonly HttpClient _httpClient;
    private readonly ILogger<JoobleApiService> _logger;
    private readonly string? _apiKey;

    public string SourceName => "jooble";

    public JoobleApiService(HttpClient httpClient, IConfiguration configuration, ILogger<JoobleApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _apiKey = configuration["JoobleApiKey"];
    }

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(_apiKey))
        {
            _logger.LogWarning("Jooble: JoobleApiKey is not configured — skipping");
            return Array.Empty<JobVacancy>();
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(45));
            var token = cts.Token;

            var locationToUse = location ?? string.Empty;
            var url = ApiBaseUrl + _apiKey;
            var allJobs = new List<JobVacancy>();
            var seenLinks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int? totalCount = null;

            for (int page = 1; page <= MaxPages; page++)
            {
                var payload = new JoobleRequest(
                    Keywords: keywords,
                    Location: locationToUse,
                    Page: page,
                    ResultOnPage: ResultsPerPage);

                var requestJson = JsonSerializer.Serialize(payload, JsonOpts);
                using var content = new StringContent(requestJson, Encoding.UTF8, "application/json");

                using var response = await _httpClient.PostAsync(url, content, token);
                var body = await response.Content.ReadAsStringAsync(token);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogError(
                        "Jooble API HTTP {Status} for keywords='{Keywords}' page={Page} — body: {Body}",
                        (int)response.StatusCode, keywords, page, Truncate(body, 300));
                    break;
                }

                if (body.Contains("\"errorCode\"", StringComparison.Ordinal))
                {
                    _logger.LogError(
                        "Jooble API returned error envelope for keywords='{Keywords}' page={Page}: {Body}",
                        keywords, page, Truncate(body, 300));
                    break;
                }

                JoobleResponse? data;
                try
                {
                    data = JsonSerializer.Deserialize<JoobleResponse>(body, JsonOpts);
                }
                catch (JsonException jx)
                {
                    _logger.LogError(jx,
                        "Jooble API: failed to deserialize response for '{Keywords}' page={Page} — body: {Body}",
                        keywords, page, Truncate(body, 300));
                    break;
                }

                totalCount ??= data?.TotalCount;
                var rawJobs = data?.Jobs ?? new List<JoobleJob>();
                if (rawJobs.Count == 0) break;

                foreach (var rj in rawJobs)
                {
                    var mapped = Map(rj);
                    if (mapped is null) continue;
                    var link = mapped.Urls.FirstOrDefault();
                    if (link is null || !seenLinks.Add(link)) continue;
                    allJobs.Add(mapped);
                }

                // Stop early if we've already covered the entire result set.
                if (totalCount is { } tc && page * ResultsPerPage >= tc) break;
            }

            _logger.LogInformation(
                "Jooble API: keywords='{Keywords}' location='{Location}' totalCount={Total} returned={Count} (pages={Pages})",
                keywords,
                string.IsNullOrEmpty(locationToUse) ? "(none)" : locationToUse,
                totalCount ?? 0,
                allJobs.Count,
                MaxPages);

            return allJobs;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jooble API error for '{Keywords}': {Message}", keywords, ex.Message);
            return Array.Empty<JobVacancy>();
        }
    }

    private static JobVacancy? Map(JoobleJob raw)
    {
        if (string.IsNullOrWhiteSpace(raw.Title) || string.IsNullOrWhiteSpace(raw.Link))
            return null;

        var title = WebUtility.HtmlDecode(raw.Title).Trim();
        var company = WebUtility.HtmlDecode(raw.Company ?? string.Empty).Trim();
        var location = string.IsNullOrWhiteSpace(raw.Location) ? null : raw.Location.Trim();
        var description = HtmlHelper.StripHtml(raw.Snippet);

        var publishedAt = DateTime.UtcNow;
        if (!string.IsNullOrWhiteSpace(raw.Updated) &&
            DateTime.TryParse(raw.Updated, out var parsed))
        {
            publishedAt = DateTime.SpecifyKind(parsed, DateTimeKind.Utc);
        }

        Salary? salary = string.IsNullOrWhiteSpace(raw.Salary) ? null : new Salary(raw.Salary.Trim());

        return JobVacancy.Create(
            title: title,
            company: company,
            url: raw.Link.Trim(),
            source: JobSource.Jooble,
            publishedAt: publishedAt,
            location: location,
            description: description,
            salary: salary);
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    private sealed record JoobleRequest(
        [property: JsonPropertyName("keywords")]     string Keywords,
        [property: JsonPropertyName("location")]     string Location,
        [property: JsonPropertyName("page")]         int Page,
        [property: JsonPropertyName("ResultOnPage")] int ResultOnPage);

    private sealed record JoobleResponse(
        [property: JsonPropertyName("totalCount")] int TotalCount,
        [property: JsonPropertyName("jobs")]       List<JoobleJob> Jobs);

    private sealed record JoobleJob(
        [property: JsonPropertyName("title")]    string? Title,
        [property: JsonPropertyName("location")] string? Location,
        [property: JsonPropertyName("snippet")]  string? Snippet,
        [property: JsonPropertyName("salary")]   string? Salary,
        [property: JsonPropertyName("source")]   string? Source,
        [property: JsonPropertyName("type")]     string? Type,
        [property: JsonPropertyName("link")]     string? Link,
        [property: JsonPropertyName("company")]  string? Company,
        [property: JsonPropertyName("updated")]  string? Updated,
        [property: JsonPropertyName("id")]       long Id);
}
