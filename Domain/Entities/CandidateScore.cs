namespace Domain.Entities;

/// <summary>
/// Result of scoring one <see cref="RecruiterCandidate"/> against one <see cref="JobVacancy"/>.
/// Unique on (VacancyId, RecruiterCandidateId) — re-analyse overwrites the existing row.
/// <see cref="ScoringResultJson"/> stores the serialised <c>Domain.Scoring.ScoringResult</c>
/// produced by the Mono engine; deserialised on read for display.
///
/// Intentionally separate from <c>ScoringCache</c>: recruiter results are tenant-isolated
/// and not shared with the candidate-side cache to avoid cross-tenant data leakage.
/// </summary>
public sealed class CandidateScore
{
    public Guid Id { get; private set; }
    public Guid VacancyId { get; private set; }
    public Guid RecruiterCandidateId { get; private set; }

    /// <summary>Composite score in [0,1]. Denormalised from <see cref="ScoringResultJson"/> for fast ordering.</summary>
    public double Score { get; private set; }

    /// <summary>Mono prompt version that produced this row. Used to detect stale scores after a prompt bump.</summary>
    public string ScoringVersion { get; private set; } = string.Empty;

    /// <summary>Full serialised <c>Domain.Scoring.ScoringResult</c> (sub-scores, reason, evidence).</summary>
    public string ScoringResultJson { get; private set; } = string.Empty;

    public DateTime ScoredAt { get; private set; }

    private CandidateScore() { }

    public static CandidateScore Create(
        Guid vacancyId,
        Guid recruiterCandidateId,
        double score,
        string scoringVersion,
        string scoringResultJson)
    {
        if (vacancyId == Guid.Empty)
            throw new ArgumentException("VacancyId cannot be empty", nameof(vacancyId));
        if (recruiterCandidateId == Guid.Empty)
            throw new ArgumentException("RecruiterCandidateId cannot be empty", nameof(recruiterCandidateId));
        if (string.IsNullOrWhiteSpace(scoringVersion))
            throw new ArgumentException("ScoringVersion cannot be empty", nameof(scoringVersion));
        if (string.IsNullOrWhiteSpace(scoringResultJson))
            throw new ArgumentException("ScoringResultJson cannot be empty", nameof(scoringResultJson));

        return new CandidateScore
        {
            Id = Guid.NewGuid(),
            VacancyId = vacancyId,
            RecruiterCandidateId = recruiterCandidateId,
            Score = Math.Clamp(score, 0.0, 1.0),
            ScoringVersion = scoringVersion,
            ScoringResultJson = scoringResultJson,
            ScoredAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Replaces the score payload — used by re-analyse to refresh an existing row.
    /// </summary>
    public void Update(double score, string scoringVersion, string scoringResultJson)
    {
        if (string.IsNullOrWhiteSpace(scoringVersion))
            throw new ArgumentException("ScoringVersion cannot be empty", nameof(scoringVersion));
        if (string.IsNullOrWhiteSpace(scoringResultJson))
            throw new ArgumentException("ScoringResultJson cannot be empty", nameof(scoringResultJson));

        Score = Math.Clamp(score, 0.0, 1.0);
        ScoringVersion = scoringVersion;
        ScoringResultJson = scoringResultJson;
        ScoredAt = DateTime.UtcNow;
    }
}
