using Domain.Enums;

namespace Domain.Entities;

public class UserProfile
{
    public Guid Id { get; private set; }
    public string Email { get; private set; } = string.Empty;
    public string? DisplayName { get; private set; }
    public string PasswordHash { get; private set; } = string.Empty;

    public string? Category { get; private set; }
    public List<string> Skills { get; private set; } = new();
    public decimal? ExpectedSalary { get; private set; }
    public WorkFormat PreferredWorkFormat { get; private set; }
    public SeniorityLevel SeniorityLevel { get; private set; }
    public string? PreferredLocation { get; private set; }

    public string? CvFileUrl { get; private set; }
    public string? CvRawText { get; private set; }

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
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
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
        UpdatedAt = DateTime.UtcNow;
    }

    public void UpdateCv(string fileName, string rawText)
    {
        CvFileUrl = fileName;
        CvRawText = rawText;
        UpdatedAt = DateTime.UtcNow;
    }
}
