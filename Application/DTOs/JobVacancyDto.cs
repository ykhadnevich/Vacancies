using Domain.Enums;

namespace Application.DTOs;

public class JobVacancyDto
{
    public Guid Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string? Location { get; init; }
    public string? Description { get; init; }
    public string? Salary { get; init; }
    public string PrimaryUrl { get; init; } = string.Empty;
    public List<string> AllUrls { get; init; } = new();
    public JobSource Source { get; init; }
    public WorkFormat WorkFormat { get; init; }
    public SeniorityLevel SeniorityLevel { get; init; }
    public string? Category { get; init; }
    public float? RelevanceScore { get; init; }
    public string? RelevanceStage { get; init; }
    public bool IsDuplicate { get; init; }
    public bool IsManuallyAdded { get; init; }
    public DateTime PublishedAt { get; init; }
}
