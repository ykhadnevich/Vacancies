using Application.Common.Enums;

namespace Application.Common.Interfaces;


public interface ICvNormalizationPromptBuilder
{


    CvNormalizationPromptResult Build(string cvRawText);


    string CurrentExpectedModelVersionPrefix { get; }
}


public sealed record CvNormalizationPromptResult(
    string Prompt,
    CvDomain DetectedDomain,
    string CompositeVersion,
    int EstimatedInputTokens);
