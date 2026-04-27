using Application.Common.Interfaces;
using HtmlAgilityPack;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Infrastructure.Helpers;

namespace Infrastructure.JobSources.Scraping;

public class LinkedInGuestService : IJobSourceService
{
    private readonly HttpClient _httpClient;
    private readonly IJobDescriptionFetcher _descriptionFetcher;

    public string SourceName => "linkedin";

    public LinkedInGuestService(HttpClient httpClient, IJobDescriptionFetcher descriptionFetcher)
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

        var url = "https://www.linkedin.com/jobs-guest/jobs/api/seeMoreJobPostings/search" +
                  $"?keywords={Uri.EscapeDataString(keywords)}" +
                  $"&location={Uri.EscapeDataString(location ?? "Ukraine")}" +
                  "&start=0&sortBy=DD";

        var html = await _httpClient.GetStringAsync(url, token);
        var jobs = ParseHtml(html);

        var enriched = await Task.WhenAll(jobs.Select(async job =>
        {
            try
            {
                var description = await _descriptionFetcher.FetchDescriptionAsync(job.PrimaryUrl, token);
                if (description != null)
                    job.UpdateDescription(HtmlHelper.ExtractLinkedInDescription(
                        HtmlHelper.StripHtml(description)));
            }
            catch { /* ігноруємо помилки опису */ }
            return job;
        }));

        return enriched.ToList();
    }

    private static IReadOnlyList<JobVacancy> ParseHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var cards = doc.DocumentNode
            .SelectNodes("//div[contains(@class,'base-card')]");

        if (cards is null) return new List<JobVacancy>();

        var jobs = new List<JobVacancy>();

        foreach (var card in cards)
        {
            var title = card.SelectSingleNode(
                ".//h3[contains(@class,'base-search-card__title')]")?.InnerText.Trim();

            var company = card.SelectSingleNode(
                ".//h4[contains(@class,'base-search-card__subtitle')]")?.InnerText.Trim();

            var location = card.SelectSingleNode(
                ".//span[contains(@class,'job-search-card__location')]")?.InnerText.Trim();

            var url = System.Net.WebUtility.HtmlDecode(
                card.SelectSingleNode(".//a")?.GetAttributeValue("href", string.Empty) ?? string.Empty);

            var dateStr = card.SelectSingleNode(".//time")
                ?.GetAttributeValue("datetime", null);

            if (string.IsNullOrEmpty(title) || string.IsNullOrEmpty(url))
                continue;

            DateTime.TryParse(dateStr, out var publishedAt);

            jobs.Add(JobVacancy.Create(
                title: title,
                company: company ?? string.Empty,
                url: url,
                source: JobSource.LinkedIn,
                publishedAt: publishedAt == default ? DateTime.UtcNow : publishedAt,
                location: location
            ));
        }

        return jobs;
    }
}