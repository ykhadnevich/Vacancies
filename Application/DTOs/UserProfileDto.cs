using Domain.Enums;

namespace Application.DTOs;

public class UserProfileDto
{
    public Guid Id { get; init; }
    public string Email { get; init; } = string.Empty;
    public string? DisplayName { get; init; }
    public string? Category { get; init; }
    public List<string> Skills { get; init; } = new();
    public decimal? ExpectedSalary { get; init; }
    public WorkFormat PreferredWorkFormat { get; init; }
    public SeniorityLevel SeniorityLevel { get; init; }
    public string? PreferredLocation { get; init; }
    public bool HasCv { get; init; }
}