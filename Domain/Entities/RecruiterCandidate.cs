using Domain.Enums;

namespace Domain.Entities;

/// <summary>
/// A single CV uploaded by a recruiter into their cabinet. Owned by the recruiter
/// (<see cref="RecruiterUserId"/>) — not by a list, not by a vacancy. Lists reference
/// candidates many-to-many through <see cref="CandidateListMembership"/>; scores against
/// vacancies live on <see cref="CandidateScore"/>.
///
/// CvHash mirrors the candidate-flow <c>Domain.Scoring.CvHasher.ComputeHash</c> contract
/// so the column is usable for de-duplication within a recruiter's pool. It is NOT shared
/// with the candidate-side <c>ScoringCache</c> — recruiter scoring results stay isolated
/// per the cabinet's privacy model.
/// </summary>
public sealed class RecruiterCandidate
{
    public Guid Id { get; private set; }
    public Guid RecruiterUserId { get; private set; }

    /// <summary>Optional friendly label the recruiter sets ("John D.", "Backend #3").</summary>
    public string? CandidateName { get; private set; }

    /// <summary>Original CV text (extracted from PDF or pasted directly).</summary>
    public string CvRawText { get; private set; } = string.Empty;

    /// <summary>
    /// JSON produced by <c>ICvExtractionService.ExtractAsync</c>. <c>null</c> until
    /// normalization completes (or if it failed). Same shape Mono consumes.
    /// </summary>
    public string? CvNormalizedJson { get; private set; }

    /// <summary>SHA-256 of the canonical CV projection. Set when normalization succeeds.</summary>
    public string? CvHash { get; private set; }

    /// <summary>Model version returned by <c>ICvExtractionService</c> on success.</summary>
    public string? NormalizationModelVersion { get; private set; }

    public CandidateNormalizationStatus Status { get; private set; } = CandidateNormalizationStatus.Pending;

    /// <summary>Last normalization error message (truncated). Null when Status != Failed.</summary>
    public string? LastError { get; private set; }

    public DateTime AddedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private RecruiterCandidate() { }

    public static RecruiterCandidate Create(
        Guid recruiterUserId,
        string cvRawText,
        string? candidateName = null)
    {
        if (recruiterUserId == Guid.Empty)
            throw new ArgumentException("RecruiterUserId cannot be empty", nameof(recruiterUserId));
        if (string.IsNullOrWhiteSpace(cvRawText))
            throw new ArgumentException("CvRawText cannot be empty", nameof(cvRawText));

        var now = DateTime.UtcNow;
        return new RecruiterCandidate
        {
            Id = Guid.NewGuid(),
            RecruiterUserId = recruiterUserId,
            CandidateName = string.IsNullOrWhiteSpace(candidateName) ? null : candidateName.Trim(),
            CvRawText = cvRawText,
            Status = CandidateNormalizationStatus.Pending,
            AddedAt = now,
            UpdatedAt = now
        };
    }

    /// <summary>
    /// Records a successful normalization pass. Transitions Status to <see cref="CandidateNormalizationStatus.Normalized"/>.
    /// </summary>
    public void MarkNormalized(string cvNormalizedJson, string cvHash, string modelVersion)
    {
        if (string.IsNullOrWhiteSpace(cvNormalizedJson))
            throw new ArgumentException("CvNormalizedJson cannot be empty", nameof(cvNormalizedJson));
        if (string.IsNullOrWhiteSpace(cvHash))
            throw new ArgumentException("CvHash cannot be empty", nameof(cvHash));
        if (string.IsNullOrWhiteSpace(modelVersion))
            throw new ArgumentException("ModelVersion cannot be empty", nameof(modelVersion));

        CvNormalizedJson = cvNormalizedJson;
        CvHash = cvHash;
        NormalizationModelVersion = modelVersion;
        Status = CandidateNormalizationStatus.Normalized;
        LastError = null;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Records a failed normalization attempt. The candidate stays in the pool so the recruiter
    /// can inspect it, but is excluded from analysis runs until re-normalized.
    /// </summary>
    public void MarkFailed(string error)
    {
        Status = CandidateNormalizationStatus.Failed;
        LastError = string.IsNullOrWhiteSpace(error)
            ? "Normalization failed"
            : error.Length > 500 ? error[..500] : error;
        UpdatedAt = DateTime.UtcNow;
    }

    public void Rename(string? candidateName)
    {
        CandidateName = string.IsNullOrWhiteSpace(candidateName) ? null : candidateName.Trim();
        UpdatedAt = DateTime.UtcNow;
    }
}
