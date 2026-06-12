namespace EvalTool;


public sealed record GoldSetCase(
    string CaseId,
    string PdfPath,
    string ExpectedJson);


public sealed record NormalizationOutput(
    string CaseId,
    string ActualJson,
    string ModelVersion,
    int InputTokens = 0,
    int OutputTokens = 0);


public sealed record CaseScores(
    string CaseId,
    Dictionary<string, double> FieldScores,
    double Overall);


public sealed record EvaluationReport(
    string Version,
    DateTime RunAt,
    List<CaseScores> PerCaseScores,
    Dictionary<string, double> PerFieldAverages,
    double Overall);
