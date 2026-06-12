namespace Application.Common.Authorization;

/// <summary>
/// Marker for MediatR requests scoped to a recruiter-owned candidate list.
/// Enforced by <c>RequireCandidateListOwnershipBehavior</c>.
/// </summary>
public interface IRequireCandidateListOwnership
{
    Guid CandidateListId { get; }
}
