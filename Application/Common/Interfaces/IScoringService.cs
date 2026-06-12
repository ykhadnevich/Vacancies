using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IScoringService
{


    Task<ScoringResult> ScoreAsync(
        string cvId,
        Guid vacancyId,
        string cvSummaryJson,
        string vacancyAnalysisJson,
        CancellationToken ct = default,
        bool skipReason = false,
        bool skipJudge = false);


    string Version { get; }
}
