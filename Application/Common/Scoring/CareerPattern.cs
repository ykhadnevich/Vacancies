namespace Application.Common.Scoring;


public sealed record CareerPattern(
    string FromRole,
    string ToRole,
    IReadOnlyList<string> RequiredSignals,
    int ScoreIfSignalsPresent,
    int ScoreIfSignalsAbsent,
    string? Note = null);
