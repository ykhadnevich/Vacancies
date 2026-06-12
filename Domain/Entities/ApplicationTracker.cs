using Domain.Constants;
using Domain.Enums;
using Microsoft.VisualBasic;

namespace Domain.Entities;

public class ApplicationTracker
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }

    public Guid? JobVacancyId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Company { get; private set; } = string.Empty;
    public string? Location { get; private set; }
    public string? Salary { get; private set; }
    public string Url { get; private set; } = string.Empty;
    public SeniorityLevel SeniorityLevel { get; private set; }

    public ApplicationStatus Status { get; private set; }

    private Dictionary<string, bool> _pipelineSteps = new();
    public IReadOnlyDictionary<string, bool> PipelineSteps => _pipelineSteps;

    public string? Notes { get; private set; }

    public DateTime AddedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }
    public bool IsManuallyAdded { get; private set; }


    public double? Score { get; private set; }


    public string? Verdict { get; private set; }

    private List<string>? _matchedSkills;
    public IReadOnlyList<string>? MatchedSkills => _matchedSkills;

    private List<string>? _missingMustHaves;
    public IReadOnlyList<string>? MissingMustHaves => _missingMustHaves;

    private List<string>? _triggeredAntiFlags;
    public IReadOnlyList<string>? TriggeredAntiFlags => _triggeredAntiFlags;


    public string? ReasonShort { get; private set; }


    public string? StrengthsEn      { get; private set; }
    public string? StrengthsUk      { get; private set; }
    public string? GapsEn           { get; private set; }
    public string? GapsUk           { get; private set; }
    public string? RecommendationEn { get; private set; }
    public string? RecommendationUk { get; private set; }


    private Dictionary<string, double>? _subScores;
    public IReadOnlyDictionary<string, double>? SubScores => _subScores;


    public string? CvFileName { get; private set; }


    public string? PipelineVersion { get; private set; }


    public DateTime? AnalyzedAt { get; private set; }

    private ApplicationTracker()
    {
        _pipelineSteps = Constants.PipelineSteps.All
            .ToDictionary(step => step, _ => false);
    }

    public static ApplicationTracker CreateFromFeed(Guid userId, JobVacancy job)
    {
        return new ApplicationTracker
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            JobVacancyId = job.Id,
            Title = job.Title,
            Company = job.Company,
            Location = job.Location,
            Salary = job.Salary?.ToString(),
            Url = job.PrimaryUrl,
            SeniorityLevel = job.SeniorityLevel,
            Status = ApplicationStatus.InReview,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsManuallyAdded = false
        };
    }

    public static ApplicationTracker CreateManually(
        Guid userId,
        string title,
        string company,
        string url,
        string? location = null,
        string? salary = null,
        SeniorityLevel seniorityLevel = SeniorityLevel.NotSpecified)
    {
        return new ApplicationTracker
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Title = title,
            Company = company,
            Location = location,
            Url = url,
            Salary = salary,
            SeniorityLevel = seniorityLevel,
            Status = ApplicationStatus.InReview,
            AddedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            IsManuallyAdded = true
        };
    }

    public void UpdateStatus(ApplicationStatus status)
    {
        Status = status;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePipelineStep(string step, bool value)
    {
        var key = _pipelineSteps.Keys.FirstOrDefault(k =>
            string.Equals(k, step, StringComparison.OrdinalIgnoreCase));

        if (key is null)
            throw new ArgumentException($"Unknown pipeline step: {step}");

        _pipelineSteps[key] = value;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateNotes(string? notes)
    {
        Notes = notes;
        UpdatedAt = DateTime.UtcNow;
    }


    public void AttachAnalysis(
        double score,
        string verdict,
        IEnumerable<string> matchedSkills,
        IEnumerable<string> missingMustHaves,
        IEnumerable<string> triggeredAntiFlags,
        string? reasonShort,
        string? strengthsEn,
        string? strengthsUk,
        string? gapsEn,
        string? gapsUk,
        string? recommendationEn,
        string? recommendationUk,
        IReadOnlyDictionary<string, double>? subScores,
        string? cvFileName,
        string pipelineVersion)
    {
        Score              = score;
        Verdict            = verdict;
        _matchedSkills     = matchedSkills.ToList();
        _missingMustHaves  = missingMustHaves.ToList();
        _triggeredAntiFlags = triggeredAntiFlags.ToList();
        ReasonShort        = reasonShort;
        StrengthsEn        = strengthsEn;
        StrengthsUk        = strengthsUk;
        GapsEn             = gapsEn;
        GapsUk             = gapsUk;
        RecommendationEn   = recommendationEn;
        RecommendationUk   = recommendationUk;
        _subScores         = subScores?.ToDictionary(kv => kv.Key, kv => kv.Value);
        CvFileName         = cvFileName;
        PipelineVersion    = pipelineVersion;
        AnalyzedAt         = DateTime.UtcNow;
        UpdatedAt          = DateTime.UtcNow;
    }
}
