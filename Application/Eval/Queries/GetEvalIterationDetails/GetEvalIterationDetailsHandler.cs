using Application.Common.Interfaces;
using Application.DTOs.Eval;
using MediatR;

namespace Application.Eval.Queries.GetEvalIterationDetails;

public sealed class GetEvalIterationDetailsHandler
    : IRequestHandler<GetEvalIterationDetailsQuery, EvalIterationDetailsDto?>
{
    private readonly IEvalIterationReader _reader;

    public GetEvalIterationDetailsHandler(IEvalIterationReader reader) => _reader = reader;

    public Task<EvalIterationDetailsDto?> Handle(
        GetEvalIterationDetailsQuery request, CancellationToken ct)
        => _reader.GetDetailsAsync(request.RunId, ct);
}
