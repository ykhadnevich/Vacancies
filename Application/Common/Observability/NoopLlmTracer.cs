using Application.Common.Interfaces;

namespace Application.Common.Observability;

/// <summary>
/// Zero-overhead default implementation. Used when LangSmith is not configured
/// or has been explicitly disabled via <c>LangSmith:Enabled=false</c>. Every
/// method is a strict no-op; the returned span performs no allocations beyond
/// the single shared <see cref="Span"/> instance.
/// </summary>
public sealed class NoopLlmTracer : ILlmTracer
{
    public static readonly NoopLlmTracer Instance = new();

    public string ProjectName => string.Empty;
    public bool IsEnabled => false;

    public ILlmSpan StartSpan(string name, LlmRunType runType, object inputs, string? parentRunId = null)
        => NoopSpan.Instance;

    private sealed class NoopSpan : ILlmSpan
    {
        public static readonly NoopSpan Instance = new();

        public string RunId => string.Empty;

        public void EndOk(object outputs) { }
        public void EndError(Exception error) { }
        public void Tag(string key, object value) { }
        public void Dispose() { }
    }
}
