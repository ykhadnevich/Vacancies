namespace Domain.Enums;

/// <summary>
/// Lifecycle of a recruiter-uploaded CV. Transitions are linear:
/// <c>Pending → Normalized</c> on success, <c>Pending → Failed</c> when the
/// LLM normalization call errors. Scoring (CandidateScore rows) is gated on
/// reaching <see cref="Normalized"/>.
/// </summary>
public enum CandidateNormalizationStatus
{
    Pending = 0,
    Normalized = 1,
    Failed = 2
}
