namespace Application.Common.Diagnostics;


public static class CostBreakdown
{
    private const double InputPricePerMillion = 0.30;
    private const double OutputPricePerMillion = 2.50;

    private static readonly AsyncLocal<Accumulator?> _current = new();

    public static IDisposable BeginScope()
    {
        _current.Value = new Accumulator();
        return new Scope();
    }

    public static void Track(string stage, double ms, long inputTokens, long outputTokens)
    {
        _current.Value?.Add(stage, ms, inputTokens, outputTokens);
    }

    public static IReadOnlyList<StageStat>? GetSnapshot() => _current.Value?.Snapshot();

    public static double EstimateCost(long inputTokens, long outputTokens)
        => (inputTokens / 1_000_000.0) * InputPricePerMillion
         + (outputTokens / 1_000_000.0) * OutputPricePerMillion;

    public sealed class StageStat
    {
        public required string Stage { get; init; }
        public required int Calls { get; init; }
        public required double TotalMs { get; init; }
        public required long TotalInputTokens { get; init; }
        public required long TotalOutputTokens { get; init; }
        public double EstimatedCost => CostBreakdown.EstimateCost(TotalInputTokens, TotalOutputTokens);
    }

    private sealed class Accumulator
    {
        private readonly object _lock = new();
        private readonly Dictionary<string, (int Calls, double Ms, long In, long Out)> _stages = new();

        public void Add(string stage, double ms, long inputTokens, long outputTokens)
        {
            lock (_lock)
            {
                if (_stages.TryGetValue(stage, out var existing))
                {
                    _stages[stage] = (existing.Calls + 1, existing.Ms + ms,
                        existing.In + inputTokens, existing.Out + outputTokens);
                }
                else
                {
                    _stages[stage] = (1, ms, inputTokens, outputTokens);
                }
            }
        }

        public IReadOnlyList<StageStat> Snapshot()
        {
            lock (_lock)
            {
                var list = new List<StageStat>(_stages.Count);
                foreach (var kv in _stages)
                {
                    list.Add(new StageStat
                    {
                        Stage = kv.Key,
                        Calls = kv.Value.Calls,
                        TotalMs = kv.Value.Ms,
                        TotalInputTokens = kv.Value.In,
                        TotalOutputTokens = kv.Value.Out
                    });
                }
                return list;
            }
        }
    }

    private sealed class Scope : IDisposable
    {
        public void Dispose() { _current.Value = null; }
    }
}
