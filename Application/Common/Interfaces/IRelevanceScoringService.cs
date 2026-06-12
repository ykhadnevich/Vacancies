namespace Application.Common.Interfaces;

public record JobScoringInput(Guid Id, string Title, string Company, string? Description);

public record RelevanceScoreResult(Guid JobId, float Score);


public interface IRelevanceScoringService
{
    Task<IReadOnlyList<RelevanceScoreResult>> ScoreJobsAsync(
        IReadOnlyList<JobScoringInput> jobs,
        string userProfileText,
        CancellationToken ct = default);
}
