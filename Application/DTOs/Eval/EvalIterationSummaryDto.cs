namespace Application.DTOs.Eval;


public sealed record EvalIterationSummaryDto(
    string RunId,
    string ModelVersion,
    DateTime GeneratedAt,
    int PairCount,
    int CvCount,
    double MeanScore,
    IReadOnlyDictionary<string, int> VerdictCounts);
