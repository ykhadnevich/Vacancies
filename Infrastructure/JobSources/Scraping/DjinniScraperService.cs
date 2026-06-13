using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;

namespace Infrastructure.JobSources.Scraping;

public class DjinniScraperService : IJobSourceService
{
    private readonly HttpClient _httpClient;

    public string SourceName => "djinni";

    public IReadOnlyList<Country> SupportedCountries => new[] { Country.Ukraine };

    // IJobDescriptionFetcher was previously injected to do a second GET per
    // card for the description text. We now pull the description out of the
    // already-loaded HTML in ExtractDescription, so the dependency is gone
    // — halving the request load on Djinni.
    public DjinniScraperService(HttpClient httpClient)
    {
        _httpClient = httpClient;
        _httpClient.DefaultRequestHeaders.Add("User-Agent",
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
    }

    // (A) Cap concurrent detail-page fetches so Djinni's anti-bot doesn't see
    //     a 60-request burst. With ParallelDetailFetchLimit=3 a typical search
    //     finishes in ~3-6 seconds while staying under the rate-limit radar.
    private const int ParallelDetailFetchLimit = 3;

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
    string keywords,
    string? location = null,
    Country country = Country.Ukraine,
    CancellationToken ct = default)
    {
    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
    var token = cts.Token;

    await Task.Delay(500, token);


    var jobCards = await CollectJobCardsAsync(keywords, token);

    if (jobCards.Count == 0)
        return new List<JobVacancy>();

    using var sem = new SemaphoreSlim(ParallelDetailFetchLimit, ParallelDetailFetchLimit);

    var jobs = await Task.WhenAll(jobCards.Select(async card =>
    {
        await sem.WaitAsync(token);
        try
        {
            var (url, applicantCount, respondsQuickly, cardPublishedAt) = card;

            // (B) Single fetch per card. We previously made two requests to the
            //     same URL: one here for title/company/salary, another via
            //     IJobDescriptionFetcher for the description. Now we parse all
            //     four out of the one HTML payload — half the load on Djinni.
            var html = await _httpClient.GetStringAsync(url, token);
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var pageTitle = doc.DocumentNode.SelectSingleNode("//title")?.InnerText ?? "";
            if (string.IsNullOrEmpty(pageTitle)) return null;

            pageTitle = pageTitle.Replace(" – Djinni", "").Trim();


            string title;
            string company;
            int sepIndex = -1;
            int sepLen = 0;
            foreach (var sep in new[] { " в ", " at " })
            {
                var idx = pageTitle.LastIndexOf(sep, StringComparison.Ordinal);
                if (idx > sepIndex)
                {
                    sepIndex = idx;
                    sepLen   = sep.Length;
                }
            }
            if (sepIndex > 0)
            {
                title   = pageTitle[..sepIndex].Trim();
                company = pageTitle[(sepIndex + sepLen)..].Trim();
            }
            else
            {
                title   = pageTitle;
                company = string.Empty;
            }

            var salary = doc.DocumentNode
                .SelectSingleNode("//*[contains(@class,'public-salary-item')]")?.InnerText.Trim();

            var job = JobVacancy.Create(
                title: title,
                company: company,
                url: url,
                source: JobSource.Djinni,
                publishedAt: cardPublishedAt ?? DateTime.UtcNow,
                salary: salary != null ? new Domain.ValueObjects.Salary(salary) : null
            );


            job.SetCompanySignals(applicantCount, respondsQuickly);

            var description = ExtractDescription(doc);
            if (!string.IsNullOrWhiteSpace(description))
                job.UpdateDescription(description);

            return job;
        }
        catch
        {
            return null;
        }
        finally
        {
            sem.Release();
        }
    }));

    return jobs.Where(j => j != null).Cast<JobVacancy>().ToList();
    }


    /// <summary>
    /// Pulls the description text out of an already-loaded Djinni job page.
    /// Tries the canonical selectors in order and falls back to any element
    /// whose class name contains "description" — defensive against Djinni's
    /// occasional CSS-class renames.
    /// </summary>
    private static string? ExtractDescription(HtmlDocument doc)
    {
        string[] candidateXPaths =
        {
            "//div[contains(@class,'job-post__description')]",
            "//div[contains(@class,'profile-page__public-info')]",
            "//div[contains(@class,'description-text')]",
            "//*[contains(@class,'mb-4') and contains(@class,'profile-page')]",
            "//*[@id='job-description']",
            "//*[contains(@class,'description') and not(self::script) and not(self::style)]",
        };

        foreach (var xpath in candidateXPaths)
        {
            var node = doc.DocumentNode.SelectSingleNode(xpath);
            if (node is null) continue;
            var raw = node.InnerHtml;
            if (string.IsNullOrWhiteSpace(raw)) continue;
            var cleaned = StripHtml(raw);
            if (!string.IsNullOrWhiteSpace(cleaned)) return cleaned;
        }
        return null;
    }


    private async Task<List<(string Url, int? ApplicantCount, bool? RespondsQuickly, DateTime? PublishedAt)>> CollectJobCardsAsync(
        string keywords, CancellationToken token)
    {
        var cards = new List<(string Url, int? ApplicantCount, bool? RespondsQuickly, DateTime? PublishedAt)>();

        for (int page = 1; page <= 4; page++)
        {
            var pageUrl = $"https://djinni.co/jobs/?all_keywords={Uri.EscapeDataString(keywords)}&search_type=basic-search&page={page}";
            var html = await _httpClient.GetStringAsync(pageUrl, token);

            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            var jobCards = doc.DocumentNode
                .SelectNodes("//div[contains(@class,'job-item') and contains(@id,'job-item-')]");

            if (jobCards is null || jobCards.Count == 0) break;

            foreach (var card in jobCards)
            {
                var link = card.SelectSingleNode(".//a[contains(@class,'job_item__header-link')]");
                var relativeUrl = link?.GetAttributeValue("href", string.Empty);
                if (string.IsNullOrEmpty(relativeUrl)) continue;

                var url = $"https://djinni.co{relativeUrl}";


                var cardText = card.InnerText;
                DateTime? publishedAt = null;


                var timeNode = card.SelectSingleNode(".//time[@datetime]");
                if (timeNode != null && DateTime.TryParse(
                    timeNode.GetAttributeValue("datetime", ""), out var timeAttr))
                    publishedAt = DateTime.SpecifyKind(timeAttr, DateTimeKind.Utc);


                if (!publishedAt.HasValue)
                {
                    var pubMatch = Regex.Match(cardText, @"[Оо]публіковано\s+(.{5,25})", RegexOptions.None);
                    if (pubMatch.Success)
                        publishedAt = UkrainianDateParser.TryParse(pubMatch.Groups[1].Value);
                }


                int? applicantCount = null;
                var countMatch = Regex.Match(cardText, @"(\d+)\s*(відгук|application|відповід)", RegexOptions.IgnoreCase);
                if (countMatch.Success && int.TryParse(countMatch.Groups[1].Value, out var cnt))
                    applicantCount = cnt;


                var cardHtml = card.OuterHtml;
                bool? respondsQuickly = null;
                if (cardHtml.Contains("Відповідає швидко", StringComparison.OrdinalIgnoreCase) ||
                    cardHtml.Contains("Actively responds", StringComparison.OrdinalIgnoreCase))
                    respondsQuickly = true;
                else if (cardHtml.Contains("Відповідає") || cardHtml.Contains("responds"))
                    respondsQuickly = false;

                cards.Add((url, applicantCount, respondsQuickly, publishedAt));
            }

            if (page < 4)
                await Task.Delay(300, token);
        }

        return cards;
    }

    private static string StripHtml(string html)
    {
        var doc = new HtmlDocument();
        doc.LoadHtml(html);
        var text = doc.DocumentNode.InnerText;
        return System.Text.RegularExpressions.Regex.Replace(text, @"\s+", " ").Trim();
    }
}
