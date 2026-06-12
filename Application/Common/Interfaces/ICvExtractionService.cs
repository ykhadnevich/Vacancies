namespace Application.Common.Interfaces;


public interface ICvExtractionService
{
    Task<CvExtractionResult> ExtractAsync(
        string cvRawText,
        CancellationToken ct = default);
}


public sealed record CvExtractionResult(
    string Summary,
    string ModelVersion,
    int InputTokens = 0,
    int OutputTokens = 0);
