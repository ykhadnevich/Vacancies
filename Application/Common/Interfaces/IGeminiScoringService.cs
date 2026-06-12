using Application.Common.Scoring;

namespace Application.Common.Interfaces;

public record GeminiJobScore(Guid JobId, float Score, string Reason, int InputTokens = 0, int OutputTokens = 0);


public interface IGeminiScoringService
{
    Task<IReadOnlyList<GeminiJobScore>> ScoreJobsAsync(
        IReadOnlyList<(Guid Id, string Title, string Company, string? Description)> jobs,
        string userProfileText,
        CancellationToken ct = default);
}
