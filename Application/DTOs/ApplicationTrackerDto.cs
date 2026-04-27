using Domain.Enums;

namespace Application.DTOs;

public class ApplicationTrackerDto
{
    public Guid Id { get; init; }
    public Guid? JobVacancyId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Company { get; init; } = string.Empty;
    public string? Salary { get; init; }
    public string Url { get; init; } = string.Empty;
    public SeniorityLevel SeniorityLevel { get; init; }
    public ApplicationStatus Status { get; init; }
    public IReadOnlyDictionary<string, bool> PipelineSteps { get; init; } 
        = new Dictionary<string, bool>();
    public string? Notes { get; init; }
    public DateTime AddedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public bool IsManuallyAdded { get; init; }
}