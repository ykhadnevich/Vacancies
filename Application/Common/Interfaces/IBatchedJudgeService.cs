using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IBatchedJudgeService
{
    Task<IReadOnlyDictionary<Guid, BatchedJudgeResult>> JudgeBatchAsync(
        string cvSummaryJson,
        IReadOnlyList<BatchedJudgeRequest> requests,
        CancellationToken ct = default);


    string Version { get; }
}


public sealed record BatchedJudgeRequest(
    Guid VacancyId,
    string VacancyAnalysisJson,
    SubScores SubScores,
    ScoringEvidence Evidence,
    double LinearScore,
    Verdict LinearVerdict);


public sealed record BatchedJudgeResult(
    double FinalScore,
    bool FallbackUsed,
    string? FailureReason);
