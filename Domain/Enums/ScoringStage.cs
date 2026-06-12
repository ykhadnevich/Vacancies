namespace Domain.Enums;

public enum ScoringStage
{
    PreFilter,
    Embedding,
    LlmRerank,
    MlBiEncoder,
    MlCrossEncoder,
    LlmCalibrated,
    Gemini
}
