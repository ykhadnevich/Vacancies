using Application.DTOs.Eval;
using MediatR;

namespace Application.Eval.Queries.GetEvalIterations;


public sealed record GetEvalIterationsQuery()
    : IRequest<IReadOnlyList<EvalIterationSummaryDto>>;
