using HtmlAgilityPack;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using System.Text.RegularExpressions;

namespace Infrastructure.JobSources.Scraping;

public class ManualUrlScraperService : IJobSourceService
{
    private readonly HttpClient _httpClient;

    public string SourceName => "manual";

    public ManualUrlScraperService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        CancellationToken ct = default)
    {
        if (!Uri.TryCreate(keywords, UriKind.Absolute, out _))
            return new List<JobVacancy>();

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = cts.Token;

        var html = await _httpClient.GetStringAsync(keywords, token);
        var jobs = ParseHtml(html, keywords);

        var enrichedJobs = new List<JobVacancy>();
        foreach (var job in jobs)
        {
            try
            {
                var jobHtml = await _httpClient.GetStringAsync(job.PrimaryUrl, token);
                var description = ExtractDescription(jobHtml);
                job.UpdateDescription(description);
            }
            catch { }
            enrichedJobs.Add(job);
        }

        return enrichedJobs;
    }

    private static string? ExtractDescription(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var selectors = new[]
        {
            "//div[contains(@class,'job-description')]",
            "//div[contains(@class,'vacancy-description')]",
            "//div[contains(@class,'description')]",
            "//div[contains(@class,'content')]",
            "//article",
            "//main",
        };

        foreach (var selector in selectors)
        {
            var node = doc.DocumentNode.SelectSingleNode(selector);
            if (node != null && node.InnerText.Trim().Length > 100)
                return node.InnerHtml.Trim();
        }

        return null;
    }

    private static string CleanTitle(string raw)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(raw);
        decoded = Regex.Replace(decoded, @"\s+", " ").Trim();
        return decoded;
    }

    private static IReadOnlyList<JobVacancy> ParseHtml(string html, string sourceUrl)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var links = doc.DocumentNode
            .SelectNodes("//a[contains(@href,'job') or contains(@href,'vacanc') or contains(@href,'career') or contains(@href,'position')]");

        if (links is null) return new List<JobVacancy>();

        var jobs = new List<JobVacancy>();
        var baseUri = new Uri(sourceUrl);
        var seen = new HashSet<string>();

        foreach (var link in links.Take(100))
        {
            var href = link.GetAttributeValue("href", string.Empty);
            var title = CleanTitle(link.InnerText);

            if (string.IsNullOrEmpty(href) || string.IsNullOrEmpty(title) || title.Length < 5)
                continue;

            if (href.StartsWith("mailto:") || href.StartsWith("#") || href.StartsWith("javascript:"))
                continue;

            if (!href.StartsWith("http"))
                href = new Uri(baseUri, href).ToString();

            if (href.TrimEnd('/') == sourceUrl.TrimEnd('/'))
                continue;

            if (!seen.Add(href))
                continue;

            jobs.Add(JobVacancy.Create(
                title: title,
                company: baseUri.Host,
                url: href,
                source: JobSource.Manual,
                publishedAt: DateTime.UtcNow,
                isManuallyAdded: true
            ));
        }

        return jobs;
    }
}
