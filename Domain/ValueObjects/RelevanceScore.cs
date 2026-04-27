using Domain.Enums;

namespace Vacancies.Domain.ValueObjects;

public sealed class RelevanceScore
{
    public float Value { get; }
    public ScoringStage Stage { get; }

    public RelevanceScore(float value, ScoringStage stage)
    {
        if (value < 0 || value > 100)
            throw new ArgumentOutOfRangeException(nameof(value), "Score must be between 0 and 100");

        Value = value;
        Stage = stage;
    }

    public string ToPercent() => $"{Value:F0}%";
}