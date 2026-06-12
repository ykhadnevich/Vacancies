using Domain.Entities;

namespace Domain.Interfaces.Repositories;

public interface IGeminiCostLogRepository
{


    Task AddRangeAsync(IEnumerable<GeminiCostLogEntry> entries, CancellationToken ct = default);


    Task<IReadOnlyList<GeminiCostLogEntry>> QueryAsync(
        DateTime from, DateTime to, CancellationToken ct = default);
}
