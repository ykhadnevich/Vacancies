using Application.Common.Diagnostics;
using Application.Common.Interfaces;
using Domain.Entities;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;


public sealed class CostLogService : ICostLogService
{
    private readonly IGeminiCostLogRepository _repo;
    private readonly ILogger<CostLogService> _logger;

    public CostLogService(
        IGeminiCostLogRepository repo,
        ILogger<CostLogService> logger)
    {
        _repo = repo;
        _logger = logger;
    }

    public async Task PersistAsync(
        Guid requestId,
        string requestKind,
        IReadOnlyList<CostBreakdown.StageStat> stages,
        Guid? userId = null,
        string? keywords = null,
        CancellationToken ct = default)
    {
        if (stages.Count == 0) return;

        var entries = stages
            .Where(s => s.Calls > 0)
            .Select(s => GeminiCostLogEntry.Create(
                requestId:    requestId,
                requestKind:  requestKind,
                stage:        s.Stage,
                calls:        s.Calls,
                durationMs:   s.TotalMs,
                inputTokens:  s.TotalInputTokens,
                outputTokens: s.TotalOutputTokens,
                costUsd:      s.EstimatedCost,
                userId:       userId,
                keywords:     keywords))
            .ToList();

        try
        {
            await _repo.AddRangeAsync(entries, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "CostLog: failed to persist {Count} entries for request {RequestId} " +
                "({Kind}). Cost data for this request will be lost.",
                entries.Count, requestId, requestKind);
        }
    }
}
