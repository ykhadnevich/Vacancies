namespace Application.Common.Interfaces;


public interface IVacancyExtractionService
{
    Task<VacancyExtractionResult> ExtractAsync(
        string vacancyRawText,
        CancellationToken ct = default);
}


public sealed record VacancyExtractionResult(
    string Json,
    string ModelVersion,
    int InputTokens = 0,
    int OutputTokens = 0);
