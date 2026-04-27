using Domain.Enums;
using Domain.ValueObjects;
using Vacancies.Domain.ValueObjects;

namespace Domain.Entities;

public class JobVacancy
{
    public Guid Id { get; private set; }

    public string Title { get; private set; } = string.Empty;
    public string Company { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string? Description { get; private set; }
    public Salary? Salary { get; private set; }

    public JobSource Source { get; private set; }
    public WorkFormat WorkFormat { get; private set; }
    public SeniorityLevel SeniorityLevel { get; private set; }
    public string? Category { get; private set; }

    public List<string> Urls { get; private set; } = new();
    public string PrimaryUrl => Urls.FirstOrDefault() ?? string.Empty;

    public RelevanceScore? RelevanceScore { get; private set; }

    public bool IsDuplicate { get; private set; }
    public Guid? CanonicalJobId { get; private set; }

    public DateTime PublishedAt { get; private set; }
    public DateTime AggregatedAt { get; private set; }
    public bool IsManuallyAdded { get; private set; }

    private JobVacancy() { }

    public static JobVacancy Create(
        string title,
        string company,
        string url,
        JobSource source,
        DateTime publishedAt,
        string? location = null,
        string? description = null,
        Salary? salary = null,
        WorkFormat workFormat = WorkFormat.NotSpecified,
        SeniorityLevel seniorityLevel = SeniorityLevel.NotSpecified,
        bool isManuallyAdded = false)
    {
        return new JobVacancy
        {
            Id = Guid.NewGuid(),
            Title = title,
            Company = company,
            Urls = new List<string> { url },
            Source = source,
            PublishedAt = publishedAt,
            AggregatedAt = DateTime.UtcNow,
            Location = location,
            Description = description,
            Salary = salary,
            WorkFormat = workFormat,
            SeniorityLevel = seniorityLevel,
            IsManuallyAdded = isManuallyAdded
        };
    }

    public void AddDuplicateUrl(string url)
    {
        if (!Urls.Contains(url))
            Urls.Add(url);
    }

    public void UpdateTitle(string title)
    {
        if (!string.IsNullOrEmpty(title))
            Title = title;
    }

    public void SetRelevanceScore(RelevanceScore score)
    {
        RelevanceScore = score;
    }

    public void MarkAsDuplicate(Guid canonicalJobId)
    {
        IsDuplicate = true;
        CanonicalJobId = canonicalJobId;
    }

    public void UpdateDescription(string? description)
    {
        if (!string.IsNullOrEmpty(description))
            Description = description;
    }

    public void SetCategory(string category)
    {
        Category = category;
    }

    public void UpdateCompany(string company)
    {
        if (!string.IsNullOrEmpty(company))
            Company = company;
    }
}
