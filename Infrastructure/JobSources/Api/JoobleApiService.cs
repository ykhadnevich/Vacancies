using HtmlAgilityPack;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Infrastructure.Helpers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.JobSources.Api;

public class JoobleApiService : IJobSourceService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JoobleApiService> _logger;

    public string SourceName => "jooble";

    public JoobleApiService(HttpClient httpClient, IConfiguration configuration, ILogger<JoobleApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        _httpClient.DefaultRequestHeaders.Add("Accept-Language", "uk-UA,uk;q=0.9,en;q=0.8");
        _httpClient.DefaultRequestHeaders.Add("Accept",
            "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
    }

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        CancellationToken ct = default)
    {
        try
        {
            var loc = Uri.EscapeDataString(location ?? "Ukraine");
            var kw = Uri.EscapeDataString(keywords);
            var url = $"https://ua.jooble.org/jobs-{kw}/{loc}";

            var html = await _httpClient.GetStringAsync(url, ct);
            var jobs = ParseHtml(html);
            _logger.LogDebug("Jooble: {Count} jobs fetched", jobs.Count);
            return jobs;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Jooble scraper error: {Message}", ex.Message);
            return new List<JobVacancy>();
        }
    }

    private static IReadOnlyList<JobVacancy> ParseHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var cards = doc.DocumentNode.SelectNodes("//div[@data-test-name='_jobCard']")
                 ?? doc.DocumentNode.SelectNodes("//article[contains(@class,'job')]")
                 ?? doc.DocumentNode.SelectNodes("//div[contains(@class,'job_card')]");


        if (cards is null) return new List<JobVacancy>();

        var jobs = new List<JobVacancy>();

        foreach (var card in cards)
        {
            var titleNode = card.SelectSingleNode(".//a[contains(@class,'job_card_link')]")
                         ?? card.SelectSingleNode(".//h2/a")
                         ?? card.SelectSingleNode(".//h3/a")
                         ?? card.SelectSingleNode(".//a[@class]");

            var title = titleNode?.InnerText.Trim();
            var href = titleNode?.GetAttributeValue("href", null);

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(href))
                continue;

            var fullUrl = href.StartsWith("http")
                ? href
                : $"https://ua.jooble.org{href}";

            fullUrl = System.Net.WebUtility.HtmlDecode(fullUrl);

            var company = card.SelectSingleNode(".//*[contains(@class,'company-name')]")?.InnerText.Trim()
                       ?? card.SelectSingleNode(".//*[contains(@class,'company')]")?.InnerText.Trim()
                       ?? string.Empty;

            var descNode = card.SelectSingleNode(".//*[contains(@class,'description')]")
                        ?? card.SelectSingleNode(".//*[contains(@class,'snippet')]");
            var description = descNode != null
                ? HtmlHelper.StripHtml(descNode.InnerHtml)
                : null;

            var location = card.SelectSingleNode(".//*[contains(@class,'location')]")?.InnerText.Trim();

            jobs.Add(JobVacancy.Create(
                title: System.Net.WebUtility.HtmlDecode(title),
                company: System.Net.WebUtility.HtmlDecode(company),
                url: fullUrl,
                source: JobSource.Jooble,
                publishedAt: DateTime.UtcNow,
                description: description,
                location: location
            ));
        }

        return jobs;
    }
}
