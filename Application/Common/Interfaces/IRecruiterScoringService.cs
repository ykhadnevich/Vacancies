using Domain.Scoring;

namespace Application.Common.Interfaces;

/// <summary>
/// Mono-engine scoring service for the recruiter cabinet. Same shape as
/// <see cref="IScoringService"/> but emits the bilingual reason in third person
/// addressed TO the recruiter ABOUT the candidate ("Кандидат має досвід у …"),
/// not in second person addressed to the candidate.
/// Implemented by <c>RecruiterMonolithicScoringService</c> with its own prompt
/// version, so the (CvHash, VacancyId, ScoringVersion) cache key never collides
/// with the candidate-side Mono cache.
/// </summary>
public interface IRecruiterScoringService
{
    Task<ScoringResult> ScoreAsync(
        string cvId,
        Guid vacancyId,
        string cvSummaryJson,
        string vacancyAnalysisJson,
        CancellationToken ct = default);

    string Version { get; }
}
