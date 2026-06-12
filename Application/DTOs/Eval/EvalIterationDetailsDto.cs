namespace Application.DTOs.Eval;


public sealed record EvalIterationDetailsDto(
    EvalIterationSummaryDto Summary,
    IReadOnlyList<EvalPairResultDto> Pairs);
