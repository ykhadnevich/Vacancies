using Domain.Enums;

namespace Domain.Entities;

public class UserProfile
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;

    /// <summary>
    /// Which cabinet(s) the user can use. Default is <see cref="UserRole.Candidate"/>
    /// — only the recruiter cabinet endpoints require this to be Recruiter or Both
    /// (enforced via the <c>RequireRecruiterBehavior</c> MediatR pipeline behavior).
    /// </summary>
    public UserRole Role { get; private set; } = UserRole.Candidate;

    public string? Category { get; private set; }
    public List<string> Skills { get; private set; } = new();
    public decimal? ExpectedSalary { get; private set; }
    public WorkFormat PreferredWorkFormat { get; private set; }
    public SeniorityLevel SeniorityLevel { get; private set; }
    public string? PreferredLocation { get; private set; }

    public string? CvFileUrl { get; private set; }
    public string? CvRawText { get; private set; }


    public string? CvFileKey { get; private set; }


    public float[]? CvEmbedding { get; private set; }


    public string? CvSummary { get; private set; }


    public string? CvSummaryModelVersion { get; private set; }


    public string? CvSkillsExpanded { get; private set; }
    public string? CvSkillsExpansionVersion { get; private set; }


    public Guid CvVersionId { get; private set; } = Guid.NewGuid();

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private UserProfile() { }

    public static UserProfile Create(string email, string passwordHash, string? displayName = null)
    {
        return new UserProfile
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = passwordHash,
            DisplayName = displayName,
            Role = UserRole.Candidate,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Switches the user's role. Downgrading from <see cref="UserRole.Recruiter"/> /
    /// <see cref="UserRole.Both"/> back to <see cref="UserRole.Candidate"/> is allowed
    /// at this layer — callers (API) decide whether the recruiter still owns vacancies
    /// that should block the transition.
    /// </summary>
    public void SetRole(UserRole role)
    {
        if (Role == role) return;
        Role = role;
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdatePreferences(
        string? displayName,
        string? category,
        List<string> skills,
        decimal? expectedSalary,
        WorkFormat workFormat,
        SeniorityLevel seniority,
        string? location)
    {
        DisplayName = displayName;
        Category = category;
        Skills = skills;
        ExpectedSalary = expectedSalary;
        PreferredWorkFormat = workFormat;
        SeniorityLevel = seniority;
        PreferredLocation = location;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCv(string fileUrl, string rawText)
    {
        CvFileUrl = fileUrl;
        CvRawText = rawText;
        CvSummary = null;
        CvSummaryModelVersion = null;
        CvSkillsExpanded = null;
        CvSkillsExpansionVersion = null;
        CvVersionId = Guid.NewGuid();
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCv(string fileName, string rawText)
    {
        CvFileUrl = fileName;
        CvRawText = rawText;
        CvSummary = null;
        CvSummaryModelVersion = null;
        CvSkillsExpanded = null;
        CvSkillsExpansionVersion = null;
        CvVersionId = Guid.NewGuid();
        UpdatedAt = DateTime.UtcNow;
    }


    public void SetCvFileKey(string fileKey)
    {
        CvFileKey = fileKey;
        UpdatedAt = DateTime.UtcNow;
    }


    public void ClearCvFileKey()
    {
        CvFileKey = null;
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetCvEmbedding(float[] embedding)
    {
        CvEmbedding = embedding;
        UpdatedAt = DateTime.UtcNow;
    }


    public void SetCvSummary(string summary, string modelVersion)
    {
        CvSummary = summary;
        CvSummaryModelVersion = modelVersion;


        CvSkillsExpanded = null;
        CvSkillsExpansionVersion = null;
        UpdatedAt = DateTime.UtcNow;
    }


    public void SetCvSkillsExpansion(string expansionJson, string expanderVersion)
    {
        if (string.IsNullOrWhiteSpace(expansionJson)) return;
        CvSkillsExpanded = expansionJson;
        CvSkillsExpansionVersion = expanderVersion;
        UpdatedAt = DateTime.UtcNow;
    }
}
