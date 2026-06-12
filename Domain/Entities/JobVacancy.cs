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


    public float[]? Embedding { get; private set; }


    public string? Reason { get; private set; }

    public bool IsDuplicate { get; private set; }
    public Guid? CanonicalJobId { get; private set; }

    public DateTime PublishedAt { get; private set; }
    public DateTime AggregatedAt { get; private set; }
    public bool IsManuallyAdded { get; private set; }

    /// <summary>
    /// Recruiter who created this vacancy through the recruiter cabinet. <c>null</c> for
    /// vacancies coming from the public aggregation pipeline (scrapers, Jooble, manual URLs).
    /// Used by <c>RequireVacancyOwnershipBehavior</c> to gate recruiter endpoints.
    /// </summary>
    public Guid? OwnerUserId { get; private set; }

    /// <summary>
    /// True when this vacancy was posted by a recruiter through the recruiter cabinet.
    /// Equivalent to <c>OwnerUserId.HasValue</c>; kept as a named accessor for readability.
    /// </summary>
    public bool IsRecruiterPosted => OwnerUserId.HasValue;


    public int? ApplicantCount { get; private set; }

    public bool? RecruiterRespondsQuickly { get; private set; }


    public string? VacancyAnalysisJson { get; private set; }

    public DateTime? VacancyAnalyzedAt { get; private set; }

    public string? VacancyAnalysisModelVersion { get; private set; }


    public string? VacancyMustHavesExpanded { get; private set; }
    public string? VacancyExpansionVersion { get; private set; }

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

    /// <summary>
    /// Marks this vacancy as owned by a recruiter. Idempotent; throws if a different
    /// owner is already assigned to prevent silent re-attribution.
    /// </summary>
    public void AssignOwner(Guid ownerUserId)
    {
        if (ownerUserId == Guid.Empty)
            throw new ArgumentException("OwnerUserId cannot be empty", nameof(ownerUserId));
        if (OwnerUserId.HasValue && OwnerUserId.Value != ownerUserId)
            throw new InvalidOperationException(
                $"Vacancy {Id} is already owned by {OwnerUserId}; cannot reassign to {ownerUserId}.");
        OwnerUserId = ownerUserId;
    }

    public void UpdateTitle(string title)
    {
        if (!string.IsNullOrEmpty(title))
            Title = title;
    }

    public void UpdateLocation(string? location)
    {
        Location = location;
    }

    public void UpdateSalary(Salary? salary)
    {
        Salary = salary;
    }

    public void UpdateWorkFormat(WorkFormat workFormat)
    {
        WorkFormat = workFormat;
    }

    public void UpdateSeniorityLevel(SeniorityLevel seniority)
    {
        SeniorityLevel = seniority;
    }

    public void SetRelevanceScore(RelevanceScore score)
    {
        RelevanceScore = score;
    }

    public void SetReason(string reason)
    {
        Reason = reason;
    }

    public void SetEmbedding(float[] embedding)
    {
        Embedding = embedding;
    }

    public void MarkAsDuplicateOf(Guid canonicalJobId)
    {
        IsDuplicate = true;
        CanonicalJobId = canonicalJobId;
    }


    public void MarkAsDuplicate(Guid canonicalJobId) => MarkAsDuplicateOf(canonicalJobId);

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


    public void SetPublishedAt(DateTime publishedAt)
    {
        if (publishedAt < DateTime.UtcNow.AddDays(1))
            PublishedAt = publishedAt;
    }

    public void SetCompanySignals(int? applicantCount, bool? respondsQuickly)
    {
        if (applicantCount.HasValue) ApplicantCount = applicantCount;
        if (respondsQuickly.HasValue) RecruiterRespondsQuickly = respondsQuickly;
    }


    public void SetVacancyAnalysis(string analysisJson, string modelVersion)
    {
        if (string.IsNullOrWhiteSpace(analysisJson)) return;
        VacancyAnalysisJson = analysisJson;
        VacancyAnalyzedAt = DateTime.UtcNow;
        VacancyAnalysisModelVersion = modelVersion;


        VacancyMustHavesExpanded = null;
        VacancyExpansionVersion = null;
    }


    public void SetVacancyMustHavesExpansion(string expansionJson, string expanderVersion)
    {
        if (string.IsNullOrWhiteSpace(expansionJson)) return;
        VacancyMustHavesExpanded = expansionJson;
        VacancyExpansionVersion = expanderVersion;
    }
}
