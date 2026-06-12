using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using HtmlAgilityPack;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace Infrastructure.JobSources;

public class JobDescriptionFetcher : IJobDescriptionFetcher
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<JobDescriptionFetcher> _logger;

    public JobDescriptionFetcher(HttpClient httpClient, ILogger<JobDescriptionFetcher> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    public async Task<string?> FetchDescriptionAsync(string url, CancellationToken ct = default)
    {
        try
        {
            var html = await _httpClient.GetStringAsync(url, ct);
            _logger.LogDebug("Fetched {Length} bytes from {Url}", html.Length, url);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);
            var node = doc.DocumentNode.SelectSingleNode(
                "//div[@id='description-wrap'] | " +
                "//div[contains(@class,'job-post-page')] | " +
                "//div[contains(@class,'vacancy-content-markup')] | " +
                "//span[@data-testid='expandable-text-box'] | " +
                "//div[contains(@class,'job-description')] | " +
                "//div[contains(@class,'description')] | " +
                "//article | " +
                "//main");

            if (node != null && node.InnerText.Trim().Length > 100)
                return node.InnerHtml.Trim();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch description from {Url}: {Message}", url, ex.Message);
        }
        return null;
    }
}
