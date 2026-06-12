namespace Application.Common.Scoring;


public sealed record AdjacencyRule(
    string FromTech,
    string ToTech,
    int PenaltyMin,
    int PenaltyMax,
    AdjacencyDirection Direction = AdjacencyDirection.Symmetric,
    string? Note = null);
