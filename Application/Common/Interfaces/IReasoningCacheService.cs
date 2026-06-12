namespace Application.Common.Interfaces;

public record PendingReasoningItem(
    Guid CvVersionId,
    Guid JobId,
    string CvText,
    string JobTitle,
    string JobDesc,
    float Score);


public record CachedReason(string Reason, float? Score);


public interface IReasoningCacheService
{


    Task<CachedReason?> GetReasonAsync(
        Guid cvVersionId,
        Guid jobId,
        CancellationToken ct = default,
        string? requiredModelVersionPrefix = null);


    Task SaveReasonAsync(
        Guid cvVersionId,
        Guid jobId,
        string reason,
        float score,
        string modelVersion,
        CancellationToken ct = default);


    Task<IReadOnlyList<PendingReasoningItem>> GetPendingItemsAsync(
        Guid cvVersionId,
        int limit,
        CancellationToken ct = default);
}
