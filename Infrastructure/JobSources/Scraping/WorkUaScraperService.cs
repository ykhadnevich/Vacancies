using Application.Common.Interfaces;
using HtmlAgilityPack;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Infrastructure.JobSources.Scraping;

public class WorkUaScraperService : IJobSourceService
{
    private readonly HttpClient _httpClient;
    private readonly IJobDescriptionFetcher _descriptionFetcher;
    private readonly ILogger<WorkUaScraperService> _logger;

    public string SourceName => "work.ua";

    public IReadOnlyList<Country> SupportedCountries => new[] { Country.Ukraine };

    public WorkUaScraperService(HttpClient httpClient, IJobDescriptionFetcher descriptionFetcher, ILogger<WorkUaScraperService> logger)
    {
        _httpClient = httpClient;
        _descriptionFetcher = descriptionFetcher;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        Country country = Country.Ukraine,
        CancellationToken ct = default)
        => await FetchJobsPageAsync(keywords, page: 1, ct);


    public async Task<IReadOnlyList<JobVacancy>> FetchJobsPageAsync(
        string keywords,
        int page,
        CancellationToken ct = default)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        var token = cts.Token;

        await Task.Delay(500, token);

        var pageSuffix = page <= 1 ? "" : $"&page={page}";
        var url = $"https://www.work.ua/jobs/?search={Uri.EscapeDataString(keywords)}{pageSuffix}";
        var html = await _httpClient.GetStringAsync(url, token);
        var jobs = ParseHtml(html);

        var enriched = await Task.WhenAll(jobs.Select(async job =>
        {
            try
            {
                var detailHtml = await _httpClient.GetStringAsync(job.PrimaryUrl, token);
                var detailDoc = new HtmlDocument();
                detailDoc.LoadHtml(detailHtml);

                var company = detailDoc.DocumentNode
                    .SelectSingleNode("//a[contains(@href,'/jobs/by-company/')]//span[contains(@class,'strong-500')]")
                    ?.InnerText.Trim();
                if (!string.IsNullOrEmpty(company))
                    job.UpdateCompany(company);

                var descNode = detailDoc.DocumentNode.SelectSingleNode("//*[@id='job-description']");
                if (descNode != null)
                    job.UpdateDescription(descNode.InnerText.Trim());


                var pageText = detailDoc.DocumentNode.InnerText;
                var dateMatch = System.Text.RegularExpressions.Regex.Match(
                    pageText, @"[Вв]акансія від (.{5,25})", System.Text.RegularExpressions.RegexOptions.None);
                if (dateMatch.Success)
                {
                    var parsed = UkrainianDateParser.TryParse(dateMatch.Groups[1].Value);
                    if (parsed.HasValue)
                        job.SetPublishedAt(parsed.Value);
                }
            }
            catch { }

            return job;
        }));

        return enriched.ToList();
    }

    private static IReadOnlyList<JobVacancy> ParseHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var container = doc.DocumentNode.SelectSingleNode("//div[@id='pjax-jobs-list']");
        var searchIn = container ?? doc.DocumentNode;

        var cards = searchIn.SelectNodes(".//div[contains(@class,'job-link')]");


        if (cards is null) return new List<JobVacancy>();

        var jobs = new List<JobVacancy>();

        foreach (var card in cards)
        {
            var titleNode = card.SelectSingleNode(".//h2/a")
                            ?? card.SelectSingleNode(".//h3/a");

            var relativeUrl = titleNode?.GetAttributeValue("href", string.Empty);

            var titleAttr = titleNode?.GetAttributeValue("title", string.Empty);
            var title = titleAttr != null && titleAttr.Contains(", вакансія")
                ? titleAttr.Split(", вакансія")[0].Trim()
                : titleNode?.InnerText.Trim();

            if (string.IsNullOrEmpty(title))
                title = titleNode?.InnerText.Split('\n')
                    .Select(s => s.Trim())
                    .FirstOrDefault(s => !string.IsNullOrEmpty(s));

            Console.WriteLine($"[WORKUA CARD] title={title} | url={relativeUrl}");

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(relativeUrl))
                continue;

            var company = card.SelectSingleNode(".//a[contains(@href,'/jobs/by-company/')]//span")?.InnerText.Trim()
                          ?? card.SelectSingleNode(".//a[contains(@href,'/jobs/by-company/')]")?.InnerText.Trim();

            jobs.Add(JobVacancy.Create(
                title: title,
                company: company ?? string.Empty,
                url: $"https://www.work.ua{relativeUrl}",
                source: JobSource.WorkUa,
                publishedAt: DateTime.UtcNow
            ));
        }

        return jobs;
    }
}
