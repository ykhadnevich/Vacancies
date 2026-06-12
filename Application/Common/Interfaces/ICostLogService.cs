using Application.Common.Diagnostics;

namespace Application.Common.Interfaces;


public interface ICostLogService
{
    Task PersistAsync(
        Guid requestId,
        string requestKind,
        IReadOnlyList<CostBreakdown.StageStat> stages,
        Guid? userId = null,
        string? keywords = null,
        CancellationToken ct = default);
}
