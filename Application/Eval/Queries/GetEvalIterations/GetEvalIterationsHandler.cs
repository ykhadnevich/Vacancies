using Application.Common.Interfaces;
using Application.DTOs.Eval;
using MediatR;

namespace Application.Eval.Queries.GetEvalIterations;


public sealed class GetEvalIterationsHandler
    : IRequestHandler<GetEvalIterationsQuery, IReadOnlyList<EvalIterationSummaryDto>>
{
    private readonly IEvalIterationReader _reader;

    public GetEvalIterationsHandler(IEvalIterationReader reader) => _reader = reader;

    public Task<IReadOnlyList<EvalIterationSummaryDto>> Handle(
        GetEvalIterationsQuery request, CancellationToken ct)
        => _reader.ListAsync(ct);
}
