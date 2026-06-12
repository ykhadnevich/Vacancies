using Domain.Entities;
using Domain.Enums;

namespace Infrastructure.JobSources.Scraping;


public static class ScrapedToDomainMapper
{


    public static JobVacancy? ToDomain(ScrapedVacancyDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Url)) return null;
        if (string.IsNullOrWhiteSpace(dto.Title)) return null;

        return JobVacancy.Create(
            title: dto.Title.Trim(),
            company: dto.Company?.Trim() ?? "",
            url: dto.Url,
            source: ParseSource(dto.Source),
            publishedAt: dto.PublishedAt == default ? DateTime.UtcNow : dto.PublishedAt.ToUniversalTime(),
            description: NormalizeDescription(dto.RawText),
            workFormat: WorkFormat.NotSpecified,
            seniorityLevel: SeniorityLevel.NotSpecified,
            isManuallyAdded: false);
    }


    public static IEnumerable<JobVacancy> ToDomainMany(IEnumerable<ScrapedVacancyDto> dtos)
    {
        foreach (var dto in dtos)
        {
            var entity = ToDomain(dto);
            if (entity is not null) yield return entity;
        }
    }


    public static JobSource ParseSource(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return JobSource.Manual;
        var key = raw.Trim().ToLowerInvariant();


        return key switch
        {
            "work.ua"    or "workua"       => JobSource.WorkUa,
            "djinni.co"  or "djinni"       => JobSource.Djinni,
            "robota.ua"  or "robotaua"     => JobSource.RobotaUa,
            "dou.ua"     or "dou"          => JobSource.DOU,
            "linkedin"   or "linkedin.com" => JobSource.LinkedIn,
            "jooble"     or "jooble.org"   => JobSource.Jooble,
            "manual"                        => JobSource.Manual,
            _                               => JobSource.Manual,
        };
    }

    private static string? NormalizeDescription(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        var trimmed = raw.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }


    public static ScrapedVacancyDto FromDomain(JobVacancy job, string searchQuery)
    {
        return new ScrapedVacancyDto
        {
            Id            = ExtractIdFromUrl(job.PrimaryUrl),
            Url           = job.PrimaryUrl,
            Source        = SourceToCanonicalString(job.Source),
            SearchQueries = new List<string> { searchQuery },
            Title         = job.Title,
            Company       = string.IsNullOrEmpty(job.Company) ? null : job.Company,
            RawText       = string.IsNullOrEmpty(job.Description) ? null : job.Description,
            Language      = "unknown",
            PublishedAt   = job.PublishedAt,
            ScrapedAt     = DateTime.UtcNow,
        };
    }


    private static string SourceToCanonicalString(JobSource source) => source switch
    {
        JobSource.WorkUa   => "work.ua",
        JobSource.Djinni   => "djinni.co",
        JobSource.RobotaUa => "robota.ua",
        JobSource.DOU      => "dou.ua",
        JobSource.LinkedIn => "linkedin.com",
        JobSource.Jooble   => "jooble.org",
        JobSource.Manual   => "manual",
        _                  => source.ToString().ToLowerInvariant(),
    };


    private static string ExtractIdFromUrl(string url)
    {
        if (string.IsNullOrEmpty(url)) return "";
        var trimmed = url.TrimEnd('/');
        var lastSlash = trimmed.LastIndexOf('/');
        return lastSlash < 0 ? trimmed : trimmed[(lastSlash + 1)..];
    }
}
