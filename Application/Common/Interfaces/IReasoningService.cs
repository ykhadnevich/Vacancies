namespace Application.Common.Interfaces;


public interface IReasoningService
{
    Task<ReasoningResult> GenerateReasonAsync(
        string cvText,
        string jobTitle,
        string jobDescription,
        float score,
        CancellationToken ct = default);
}


public sealed record ReasoningResult(
    string Reason,
    string ModelVersion,
    float? Score = null,
    int InputTokens = 0,
    int OutputTokens = 0);
