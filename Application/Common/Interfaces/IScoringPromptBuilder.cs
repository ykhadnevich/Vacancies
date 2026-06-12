using Application.Common.Scoring;
using Domain.Scoring;

namespace Application.Common.Interfaces;


public interface IScoringPromptBuilder
{
    PromptBuildResult Build(ScoringPromptContext ctx);
}


public sealed record PromptBuildResult(
    string Prompt,
    RoleFamily DetectedFamily,
    string CompositeVersion,
    int EstimatedInputTokens);
