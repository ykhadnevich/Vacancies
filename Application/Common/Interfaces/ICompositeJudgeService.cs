using System.Text.Json;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface ICompositeJudgeService
{
    Task<JudgeResult> JudgeAsync(
        JsonElement cvSummary,
        JsonElement vacancyAnalysis,
        SubScores subScores,
        ScoringEvidence evidence,
        double initialScore,
        Verdict initialVerdict,
        CancellationToken ct = default);
}


public sealed record JudgeResult(
    double FinalScore,
    Domain.Scoring.Verdict FinalVerdict,
    int InputTokens,
    int OutputTokens,
    bool FallbackUsed,
    string? FailureReason);
