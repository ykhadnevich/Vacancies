namespace Domain.Entities;

/// <summary>
/// A named pool of candidates owned by a recruiter. Acts purely as a grouping container —
/// candidates live in <see cref="RecruiterCandidate"/>, the link is the
/// <see cref="CandidateListMembership"/> join. Lists can be analysed against multiple vacancies;
/// candidates can sit in multiple lists.
/// </summary>
public sealed class CandidateList
{
    public Guid Id { get; private set; }
    public Guid RecruiterUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private CandidateList() { }

    public static CandidateList Create(Guid recruiterUserId, string name, string? description = null)
    {
        if (recruiterUserId == Guid.Empty)
            throw new ArgumentException("RecruiterUserId cannot be empty", nameof(recruiterUserId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));

        var now = DateTime.UtcNow;
        return new CandidateList
        {
            Id = Guid.NewGuid(),
            RecruiterUserId = recruiterUserId,
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            CreatedAt = now,
            UpdatedAt = now
        };
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty", nameof(name));
        Name = name.Trim();
        UpdatedAt = DateTime.UtcNow;
    }

    public void SetDescription(string? description)
    {
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
