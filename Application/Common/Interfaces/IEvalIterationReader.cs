using Application.DTOs.Eval;

namespace Application.Common.Interfaces;


public interface IEvalIterationReader
{


    Task<IReadOnlyList<EvalIterationSummaryDto>> ListAsync(CancellationToken ct = default);


    Task<EvalIterationDetailsDto?> GetDetailsAsync(string runId, CancellationToken ct = default);
}
