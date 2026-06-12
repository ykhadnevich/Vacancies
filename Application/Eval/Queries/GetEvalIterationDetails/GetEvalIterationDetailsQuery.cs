using Application.DTOs.Eval;
using MediatR;

namespace Application.Eval.Queries.GetEvalIterationDetails;


public sealed record GetEvalIterationDetailsQuery(string RunId)
    : IRequest<EvalIterationDetailsDto?>;
