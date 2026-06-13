using System.ServiceModel.Syndication;
using System.Xml;
using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces.Services;
using Infrastructure.Helpers;

namespace Infrastructure.JobSources.Rss;

public class DouRssFeedService : IJobSourceService
{
    public string SourceName => "dou";

    public IReadOnlyList<Country> SupportedCountries => new[] { Country.Ukraine };

    public async Task<IReadOnlyList<JobVacancy>> FetchJobsAsync(
        string keywords,
        string? location = null,
        Country country = Country.Ukraine,
        CancellationToken ct = default)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var token = cts.Token;

        var url = BuildFeedUrl(keywords, location);

        return await Task.Run(() =>
        {
            using var reader = XmlReader.Create(url);
            var feed = SyndicationFeed.Load(reader);

            return feed.Items
                .Select(MapToJobVacancy)
                .ToList() as IReadOnlyList<JobVacancy>;
        }, token);
    }

    private static string BuildFeedUrl(string keywords, string? location)
    {
        var category = keywords.ToLower() switch
        {
            var k when k.Contains(".net")                            => ".NET",
            var k when k.Contains("golang") || k.Contains("go ")    => "Go",
            var k when k.Contains("python")                          => "Python",
            var k when k.Contains("javascript") || k.Contains("js") => "JavaScript",
            var k when k.Contains("java")                            => "Java",
            var k when k.Contains("qa") || k.Contains("test")       => "QA",
            var k when k.Contains("design")                          => "Design",
            var k when k.Contains("product")                         => "Product+Management",
            var k when k.Contains("data")                            => "Data+Science",
            _                                                        => Uri.EscapeDataString(keywords)
        };

        var url = $"https://jobs.dou.ua/vacancies/feeds/?category={category}";

        if (!string.IsNullOrEmpty(location))
            url += $"&city={Uri.EscapeDataString(location)}";

        return url;
    }

    private static JobVacancy MapToJobVacancy(SyndicationItem item)
    {
        var link = item.Links.FirstOrDefault()?.Uri.ToString() ?? string.Empty;
        var company = item.Authors.FirstOrDefault()?.Name ?? string.Empty;

        var rawTitle = item.Title.Text;
        var title = rawTitle.Contains(" в ")
            ? rawTitle.Split(" в ")[0].Trim()
            : rawTitle;

        if (string.IsNullOrEmpty(company) && rawTitle.Contains(" в "))
            company = rawTitle.Split(" в ")[1].Split(',')[0].Trim();

        return JobVacancy.Create(
            title: title,
            company: company,
            url: link,
            source: JobSource.DOU,
            publishedAt: item.PublishDate.DateTime,
            description: HtmlHelper.StripHtml(item.Summary?.Text)
        );
    }
}
