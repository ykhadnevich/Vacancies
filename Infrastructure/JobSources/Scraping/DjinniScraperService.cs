using Application.Common.Interfaces;
using HtmlAgilityPack;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;

namespace Infrastructure.JobSources.Scraping;

public class DjinniScraperService : IJobSourceService
{
    private readonly HttpClient _httpClient;
    private readonly IJobDescriptionFetcher _descriptionFetcher;

    public string SourceName => "djinni";

    public DjinniScraperService(HttpClient httpClient, IJobDescriptionFetcher descriptionFetcher)
    {
        _httpClient = httpClient;
        _descriptionFetcher = descriptionFetcher;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
    string keywords,
    string? location = null,
    CancellationToken ct = default)
    {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var token = cts.Token;

    await Task.Delay(500, token);

    var jobUrls = await CollectJobUrlsAsync(keywords, token);

    if (jobUrls.Count == 0)
        return new List<JobVacancy>();

    var jobs = await Task.WhenAll(jobUrls.Select(async url =>
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url, token);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "";
            if (string.IsNullOrEmpty(pageTitle)) return null;

            pageTitle = pageTitle.Replace(" – Djinni", "").Trim();
            var title = pageTitle.Contains(" в ")
                ? pageTitle.Split(" в ")[0].Trim()
                : pageTitle;

            var company = pageTitle.Contains(" в ")
                ? pageTitle.Split(" в ")[1].Trim()
                : string.Empty;

            var salary = doc.DocumentNode
                .SelectSingleNode("//*[contains(@class,'public-salary-item')]")?.InnerText.Trim();

            var job = JobVacancy.Create(
                title: title,
                company: company,
                url: url,
                source: JobSource.Djinni,
                publishedAt: DateTime.UtcNow,
                salary: salary != null ? new Domain.ValueObjects.Salary(salary) : null
            );

            var description = await _descriptionFetcher.FetchDescriptionAsync(url, token);
            if (description != null)
                job.UpdateDescription(StripHtml(description));

            return job;
        }
        catch
        {
            return null;
        }
    }));

    return jobs.Where(j => j != null).Cast<JobVacancy>().ToList();
    }

    private async Task<List<string>> CollectJobUrlsAsync(string keywords, CancellationToken token)
    {
        var urls = new List<string>();

        for (int page = 1; page <= 4; page++)
        {
            var pageUrl = $"https://djinni.co/jobs/?all_keywords={Uri.EscapeDataString(keywords)}&search_type=basic-search&page={page}";
            var html = await _httpClient.GetStringAsync(pageUrl, token);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var cards = doc.DocumentNode
                .SelectNodes("//div[contains(@class,'job-item') and contains(@id,'job-item-')]");

            if (cards is null || cards.Count == 0) break;

            foreach (var card in cards)
            {
                var link = card.SelectSingleNode(".//a[contains(@class,'job_item__header-link')]");
                var relativeUrl = link?.GetAttributeValue("href", null);
                if (!string.IsNullOrEmpty(relativeUrl))
                    urls.Add($"https://djinni.co{relativeUrl}");
            }

            if (page < 4)
                await Task.Delay(300, token);
        }

        return urls;
    }

    private static string StripHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var text = doc.DocumentNode.InnerText;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}