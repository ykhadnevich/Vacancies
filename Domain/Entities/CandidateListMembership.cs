namespace Domain.Entities;

/// <summary>
/// Many-to-many link between <see cref="CandidateList"/> and <see cref="RecruiterCandidate"/>.
/// Composite primary key on (CandidateListId, RecruiterCandidateId) so duplicate adds are
/// rejected at the database layer. Same candidate can sit in multiple lists.
/// </summary>
public sealed class CandidateListMembership
{
    public Guid CandidateListId { get; private set; }
    public Guid RecruiterCandidateId { get; private set; }
    public DateTime AddedAt { get; private set; }

    private CandidateListMembership() { }

    public static CandidateListMembership Create(Guid candidateListId, Guid recruiterCandidateId)
    {
        if (candidateListId == Guid.Empty)
            throw new ArgumentException("CandidateListId cannot be empty", nameof(candidateListId));
        if (recruiterCandidateId == Guid.Empty)
            throw new ArgumentException("RecruiterCandidateId cannot be empty", nameof(recruiterCandidateId));

        return new CandidateListMembership
        {
            CandidateListId = candidateListId,
            RecruiterCandidateId = recruiterCandidateId,
            AddedAt = DateTime.UtcNow
        };
    }
}
