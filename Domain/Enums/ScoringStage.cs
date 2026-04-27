namespace Domain.Enums;

public enum ScoringStage
{
    PreFilter,   // Stage 1 — власний алгоритм
    Embedding,   // Stage 2 — cosine similarity
    LlmRerank    // Stage 3 — LLM
}