using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface IVacancyNormalizationPromptBuilder
{


    VacancyNormalizationPromptResult Build(string vacancyRawText);


    string CurrentExpectedModelVersionPrefix { get; }
}


public sealed record VacancyNormalizationPromptResult(
    string Prompt,
    VacancyDomain DetectedDomain,
    string CompositeVersion,
    int EstimatedInputTokens);
