namespace Application.Common.Interfaces;

/// <summary>
/// Scrapes a single recruiter-provided vacancy URL and returns the extracted
/// title / company / description. Used by <c>CreateRecruiterVacancyFromUrlCommand</c>.
///
/// Distinct from <c>IJobSourceService</c> (which crawls listing pages and returns
/// many vacancies). This service targets ONE specific vacancy page and tries
/// domain-specific selectors (Djinni, DOU, Work.ua, Robota.ua, LinkedIn) before
/// falling back to OpenGraph meta tags and finally the page <c>&lt;title&gt;</c>.
/// </summary>
public interface IRecruiterVacancyScraper
{
    Task<RecruiterVacancyScrapeResult?> ScrapeAsync(string url, CancellationToken ct = default);
}

public sealed record RecruiterVacancyScrapeResult(
    string Title,
    string Company,
    string? Location,
    string Description,
    /// <summary>Indicates which selector path produced the data — diagnostic only.</summary>
    string SourceHint);
