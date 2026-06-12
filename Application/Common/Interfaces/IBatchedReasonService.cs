using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IBatchedReasonService
{
    Task<IReadOnlyDictionary<Guid, BatchedReasonResult>> GenerateBatchAsync(
        IReadOnlyList<BatchedReasonRequest> requests,
        CancellationToken ct = default);


    string Version { get; }
}


public sealed record BatchedReasonRequest(
    Guid VacancyId,
    string VacancyTitle,
    Verdict Verdict,
    double Score,
    SubScores SubScores,
    ScoringEvidence Evidence,
    ReasonContext Context);


public sealed record BatchedReasonResult(
    string StrengthsEn,
    string StrengthsUk,
    string GapsEn,
    string GapsUk,
    string RecommendationEn,
    string RecommendationUk);
