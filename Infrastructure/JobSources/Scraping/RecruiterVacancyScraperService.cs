using System.Net;
using System.Text.RegularExpressions;
using Application.Common.Interfaces;
using HtmlAgilityPack;
using Microsoft.Extensions.Logging;

namespace Infrastructure.JobSources.Scraping;

/// <summary>
/// Scrapes a single vacancy page URL pasted by a recruiter. Tries domain-specific
/// selectors first (Djinni / DOU / Work.ua / Robota.ua / LinkedIn) then falls
/// back to OpenGraph meta tags, then to the bare <c>&lt;title&gt;</c> + body text.
///
/// Each handler returns null on miss so the next strategy is attempted. This
/// keeps the service resilient against minor HTML changes on the source sites.
/// </summary>
public sealed class RecruiterVacancyScraperService : IRecruiterVacancyScraper
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<RecruiterVacancyScraperService> _logger;

    public RecruiterVacancyScraperService(
        HttpClient httpClient,
        ILogger<RecruiterVacancyScraperService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;

        if (!_httpClient.DefaultRequestHeaders.UserAgent.Any())
        {
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0 Safari/537.36");
            _httpClient.DefaultRequestHeaders.TryAddWithoutValidation(
                "Accept-Language", "uk,en;q=0.8");
        }
    }

    public async Task<RecruiterVacancyScrapeResult?> ScrapeAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            return null;

        string html;
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(TimeSpan.FromSeconds(20));
            html = await _httpClient.GetStringAsync(url, cts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "RecruiterVacancyScraper: HTTP fetch failed for {Url}.", url);
            return null;
        }

        var doc = new HtmlDocument();
        doc.LoadHtml(html);

        var host = uri.Host.ToLowerInvariant();

        // Domain-specific handlers in priority order. Each returns null if its
        // expected selectors don't match — the next strategy then gets a chance.
        var attempts = new (string Hint, Func<HtmlDocument, Uri, RecruiterVacancyScrapeResult?> Run)[]
        {
            ("djinni",     TryDjinni),
            ("dou",        TryDou),
            ("workua",     TryWorkUa),
            ("robotaua",   TryRobotaUa),
            ("linkedin",   TryLinkedIn),
            ("opengraph",  TryOpenGraph),
            ("fallback",   TryFallback),
        };

        foreach (var (hint, run) in attempts)
        {
            // Only run domain handlers when the host matches — keeps generic strategies
            // (OpenGraph, fallback) from being skipped when the URL is on an unknown domain.
            var allowedDomainOnly =
                hint == "djinni"   ? host.Contains("djinni") :
                hint == "dou"      ? host.Contains("dou.ua") :
                hint == "workua"   ? host.Contains("work.ua") :
                hint == "robotaua" ? host.Contains("robota") :
                hint == "linkedin" ? host.Contains("linkedin") :
                true;

            if (!allowedDomainOnly) continue;

            var result = run(doc, uri);
            if (result is not null && IsUsable(result))
            {
                _logger.LogInformation(
                    "RecruiterVacancyScraper: matched {Hint} for {Url} — title='{Title}', desc={Len} chars.",
                    hint, url, result.Title, result.Description.Length);
                return result;
            }
        }

        _logger.LogWarning("RecruiterVacancyScraper: all strategies missed for {Url}.", url);
        return null;
    }

    // ─── Djinni ─────────────────────────────────────────────────────────────
    // URL pattern: https://djinni.co/jobs/<id>-<slug>/
    private static RecruiterVacancyScrapeResult? TryDjinni(HtmlDocument doc, Uri uri)
    {
        var title = First(doc,
            "//h1[contains(@class,'detail--title-wrapper')]",
            "//h1[contains(@class,'job-details__title')]",
            "//h1");
        var company = First(doc,
            "//a[contains(@class,'job-details--title')]",
            "//div[contains(@class,'job-details--detail')]/a",
            "//a[contains(@href,'/jobs/?company=')]");
        var description = FirstHtml(doc,
            "//div[contains(@class,'mb-4') and contains(@class,'job-post__description')]",
            "//div[contains(@class,'profile-page-section')]",
            "//section[contains(@class,'card')]");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        return new RecruiterVacancyScrapeResult(
            Clean(title),
            string.IsNullOrWhiteSpace(company) ? "Djinni" : Clean(company),
            null,
            description,
            "djinni");
    }

    // ─── DOU.ua ─────────────────────────────────────────────────────────────
    // URL pattern: https://jobs.dou.ua/companies/<slug>/vacancies/<id>/
    private static RecruiterVacancyScrapeResult? TryDou(HtmlDocument doc, Uri uri)
    {
        var title = First(doc,
            "//h1[contains(@class,'g-h2')]",
            "//article//h1",
            "//div[contains(@class,'vacancy')]//h1",
            "//h1");
        var company = First(doc,
            "//div[contains(@class,'l-n')]//a",
            "//div[contains(@class,'b-compinfo')]//a[contains(@class,'company-name')]",
            "//a[contains(@href,'/companies/')]");
        var location = First(doc,
            "//span[contains(@class,'place')]",
            "//div[contains(@class,'sh-info')]//span[contains(@class,'place')]");
        var description = FirstHtml(doc,
            "//div[contains(@class,'b-typo') and contains(@class,'vacancy-section')]",
            "//div[contains(@class,'b-typo')]",
            "//div[contains(@class,'text')]");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        var companyClean = string.IsNullOrWhiteSpace(company)
            ? "DOU"
            : Clean(company);

        return new RecruiterVacancyScrapeResult(
            Clean(title),
            companyClean,
            string.IsNullOrWhiteSpace(location) ? null : Clean(location),
            description,
            "dou");
    }

    // ─── Work.ua ────────────────────────────────────────────────────────────
    // URL pattern: https://www.work.ua/jobs/<id>/
    private static RecruiterVacancyScrapeResult? TryWorkUa(HtmlDocument doc, Uri uri)
    {
        var title = First(doc,
            "//h1[@id='h1-name']",
            "//h1[contains(@class,'add-bottom-sm')]",
            "//h1");
        var company = First(doc,
            "//a[contains(@class,'card-vacancy__company-name')]",
            "//span[@itemprop='name']",
            "//div[contains(@class,'card')]//a[contains(@href,'/employer/')]");
        var location = First(doc,
            "//span[contains(@class,'glyphicon-map-marker')]/following-sibling::span",
            "//span[contains(@class,'add-top-xs')]");
        var description = FirstHtml(doc,
            "//div[@id='job-description']",
            "//div[contains(@class,'job-description')]",
            "//div[contains(@class,'b-typo')]");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        return new RecruiterVacancyScrapeResult(
            Clean(title),
            string.IsNullOrWhiteSpace(company) ? "Work.ua" : Clean(company),
            string.IsNullOrWhiteSpace(location) ? null : Clean(location),
            description,
            "workua");
    }

    // ─── Robota.ua ──────────────────────────────────────────────────────────
    // URL pattern: https://robota.ua/company<n>/vacancy<id>  (heavily JS-rendered)
    private static RecruiterVacancyScrapeResult? TryRobotaUa(HtmlDocument doc, Uri uri)
    {
        // Robota.ua serves a SPA with most content rendered client-side. We rely
        // on whatever the server-rendered shell ships (typically OpenGraph for SEO).
        var title = First(doc,
            "//h1",
            "//meta[@property='og:title']/@content");
        var company = First(doc,
            "//a[contains(@href,'/company')]",
            "//meta[@property='og:site_name']/@content");
        var description = FirstHtml(doc,
            "//div[contains(@class,'description')]",
            "//meta[@property='og:description']/@content");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        return new RecruiterVacancyScrapeResult(
            Clean(title),
            string.IsNullOrWhiteSpace(company) ? "Robota.ua" : Clean(company),
            null,
            description,
            "robotaua");
    }

    // ─── LinkedIn (public guest view) ───────────────────────────────────────
    // URL pattern: https://www.linkedin.com/jobs/view/<id>/
    private static RecruiterVacancyScrapeResult? TryLinkedIn(HtmlDocument doc, Uri uri)
    {
        var title = First(doc,
            "//h1[contains(@class,'top-card-layout__title')]",
            "//h1[contains(@class,'topcard__title')]",
            "//h1");
        var company = First(doc,
            "//a[contains(@class,'topcard__org-name-link')]",
            "//span[contains(@class,'topcard__flavor')]/a");
        var location = First(doc,
            "//span[contains(@class,'topcard__flavor--bullet')]");
        var description = FirstHtml(doc,
            "//div[contains(@class,'show-more-less-html__markup')]",
            "//div[contains(@class,'description__text')]",
            "//section[contains(@class,'show-more-less-html')]");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        return new RecruiterVacancyScrapeResult(
            Clean(title),
            string.IsNullOrWhiteSpace(company) ? "LinkedIn" : Clean(company),
            string.IsNullOrWhiteSpace(location) ? null : Clean(location),
            description,
            "linkedin");
    }

    // ─── OpenGraph fallback (any site that ships <meta og:*>) ───────────────
    private static RecruiterVacancyScrapeResult? TryOpenGraph(HtmlDocument doc, Uri uri)
    {
        var title       = MetaContent(doc, "og:title");
        var description = MetaContent(doc, "og:description");
        var siteName    = MetaContent(doc, "og:site_name");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
            return null;

        return new RecruiterVacancyScrapeResult(
            Clean(title!),
            string.IsNullOrWhiteSpace(siteName) ? uri.Host : Clean(siteName!),
            null,
            description!,
            "opengraph");
    }

    // ─── Last-resort fallback (page <title> + body text) ────────────────────
    private static RecruiterVacancyScrapeResult? TryFallback(HtmlDocument doc, Uri uri)
    {
        var title = First(doc, "//title");
        var body  = First(doc, "//main", "//article", "//body");

        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(body) || body.Trim().Length < 100)
            return null;

        return new RecruiterVacancyScrapeResult(
            Clean(title),
            uri.Host,
            null,
            body,
            "fallback");
    }

    // ─── Helpers ────────────────────────────────────────────────────────────

    /// <summary>Returns the first non-empty InnerText match among the supplied XPaths.</summary>
    private static string? First(HtmlDocument doc, params string[] xpaths)
    {
        foreach (var xp in xpaths)
        {
            // Allow trailing "/@attribute" → read attribute instead of inner text.
            string? attribute = null;
            var path = xp;
            var attrIdx = xp.IndexOf("/@", StringComparison.Ordinal);
            if (attrIdx > 0)
            {
                attribute = xp[(attrIdx + 2)..];
                path = xp[..attrIdx];
            }

            var node = doc.DocumentNode.SelectSingleNode(path);
            if (node is null) continue;

            var value = attribute is not null
                ? node.GetAttributeValue(attribute, string.Empty)
                : node.InnerText;
            if (!string.IsNullOrWhiteSpace(value)) return value;
        }
        return null;
    }

    /// <summary>Returns the first non-empty InnerHtml match (preserves formatting).</summary>
    private static string? FirstHtml(HtmlDocument doc, params string[] xpaths)
    {
        foreach (var xp in xpaths)
        {
            string? attribute = null;
            var path = xp;
            var attrIdx = xp.IndexOf("/@", StringComparison.Ordinal);
            if (attrIdx > 0)
            {
                attribute = xp[(attrIdx + 2)..];
                path = xp[..attrIdx];
            }

            var node = doc.DocumentNode.SelectSingleNode(path);
            if (node is null) continue;

            var value = attribute is not null
                ? node.GetAttributeValue(attribute, string.Empty)
                : node.InnerHtml;
            if (!string.IsNullOrWhiteSpace(value) && WebUtility.HtmlDecode(StripTags(value)).Trim().Length >= 50)
                return value;
        }
        return null;
    }

    private static string? MetaContent(HtmlDocument doc, string property)
    {
        var node = doc.DocumentNode.SelectSingleNode(
            $"//meta[@property='{property}'] | //meta[@name='{property}']");
        var raw = node?.GetAttributeValue("content", string.Empty);
        return string.IsNullOrEmpty(raw) ? null : raw;
    }

    private static string Clean(string raw)
    {
        var decoded = WebUtility.HtmlDecode(StripTags(raw));
        decoded = Regex.Replace(decoded, @"\s+", " ").Trim();
        return decoded;
    }

    private static string StripTags(string html) => Regex.Replace(html, "<[^>]*>", " ");

    private static bool IsUsable(RecruiterVacancyScrapeResult r)
    {
        if (string.IsNullOrWhiteSpace(r.Title) || r.Title.Length < 3) return false;
        if (string.IsNullOrWhiteSpace(r.Description) || WebUtility.HtmlDecode(StripTags(r.Description)).Trim().Length < 50)
            return false;
        return true;
    }
}
