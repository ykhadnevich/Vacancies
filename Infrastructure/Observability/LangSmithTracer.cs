using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Observability;

/// <summary>
/// LangSmith implementation of <see cref="ILlmTracer"/>. Posts run-create /
/// run-update events to <c>api.smith.langchain.com</c> via fire-and-forget
/// background channel — Gemini hot path is never blocked on the network call
/// to the observability backend.
///
/// Failure semantics: any HTTP error / serialization issue is logged at
/// Warning level and silently dropped — tracing must never poison a real
/// production call.
/// </summary>
public sealed class LangSmithTracer : ILlmTracer, IHostedService, IAsyncDisposable
{
    private readonly HttpClient _http;
    private readonly ILogger<LangSmithTracer> _logger;
    private readonly Channel<TraceEvent> _events;
    private readonly CancellationTokenSource _cts = new();
    private readonly string _apiKey;
    private Task? _pump;

    public string ProjectName { get; }
    public bool IsEnabled => true;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public LangSmithTracer(
        HttpClient http,
        IConfiguration configuration,
        ILogger<LangSmithTracer> logger)
    {
        _http = http;
        _logger = logger;

        var section = configuration.GetSection("LangSmith");
        _apiKey      = section["ApiKey"]   ?? string.Empty;
        ProjectName  = section["Project"]  ?? "vakansio";
        var endpoint = section["Endpoint"] ?? "https://api.smith.langchain.com";

        _http.BaseAddress = new Uri(endpoint);
        _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);

        // Unbounded channel — events are tiny POST bodies, throughput is well under
        // 1000/sec even at peak v6 query. SingleReader optimisation since the pump is one Task.
        _events = Channel.CreateUnbounded<TraceEvent>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ILlmSpan StartSpan(string name, LlmRunType runType, object inputs, string? parentRunId = null)
    {
        var runId = Guid.NewGuid().ToString();
        var startedAt = DateTime.UtcNow;

        // Enqueue run-create. If channel is closed (shutdown) we silently drop —
        // observability MUST NOT throw on hot path.
        _events.Writer.TryWrite(new TraceEvent(
            Kind: TraceEventKind.Create,
            RunId: runId,
            Payload: new Dictionary<string, object?>
            {
                ["id"]            = runId,
                ["name"]          = name,
                ["run_type"]      = runType.ToString().ToLowerInvariant(),
                ["start_time"]    = startedAt,
                ["inputs"]        = inputs,
                ["session_name"]  = ProjectName,
                ["parent_run_id"] = parentRunId
            }));

        return new Span(this, runId, startedAt);
    }

    // ─── IHostedService ───────────────────────────────────────────────────────

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _pump = Task.Run(() => PumpAsync(_cts.Token), CancellationToken.None);
        _logger.LogInformation(
            "LangSmith tracer started — project='{Project}', endpoint='{Endpoint}'.",
            ProjectName, _http.BaseAddress);
        return Task.CompletedTask;
    }

    private int _stopped;  // 0 = running, 1 = StopAsync called, 2 = disposed

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopped, 1) != 0) return;

        // Complete the writer so the pump exits naturally after draining,
        // then cancel the read loop as a backstop in case the pump is mid-await.
        _events.Writer.TryComplete();
        try { _cts.Cancel(); } catch (ObjectDisposedException) { /* race with Dispose */ }

        if (_pump is not null)
        {
            // Up to 3 s for drain — don't block container shutdown on a slow
            // LangSmith endpoint. Pass CancellationToken.None so the cancellation
            // signal that triggered the shutdown does not also abort the drain.
            try
            {
                await Task.WhenAny(_pump, Task.Delay(3000, CancellationToken.None));
            }
            catch { /* shutdown is best-effort */ }
        }
    }

    public ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _stopped, 2) == 2) return ValueTask.CompletedTask;
        try { _cts.Cancel(); } catch (ObjectDisposedException) { /* already disposed */ }
        _cts.Dispose();
        return ValueTask.CompletedTask;
    }

    // ─── Pump ─────────────────────────────────────────────────────────────────

    private async Task PumpAsync(CancellationToken ct)
    {
        try
        {
            await foreach (var ev in _events.Reader.ReadAllAsync(ct))
            {
                try
                {
                    var path = ev.Kind == TraceEventKind.Create ? "runs" : $"runs/{ev.RunId}";
                    var method = ev.Kind == TraceEventKind.Create ? HttpMethod.Post : HttpMethod.Patch;

                    using var req = new HttpRequestMessage(method, path)
                    {
                        Content = JsonContent.Create(ev.Payload, options: JsonOpts)
                    };

                    var resp = await _http.SendAsync(req, ct);
                    if (!resp.IsSuccessStatusCode)
                    {
                        var body = await resp.Content.ReadAsStringAsync(ct);
                        _logger.LogWarning(
                            "LangSmith trace dropped: {Kind} {RunId} → HTTP {Status} {Body}",
                            ev.Kind, ev.RunId, (int)resp.StatusCode, Trim(body, 400));
                    }
                }
                catch (OperationCanceledException) { /* shutdown */ }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "LangSmith trace failed for {Kind} {RunId}.", ev.Kind, ev.RunId);
                }
            }
        }
        catch (OperationCanceledException) { /* shutdown */ }
    }

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty : (s.Length <= max ? s : s[..max] + "…");

    // ─── Internal helpers used by Span ────────────────────────────────────────

    private void Enqueue(TraceEvent ev) => _events.Writer.TryWrite(ev);

    // ─── Span ─────────────────────────────────────────────────────────────────

    private enum TraceEventKind { Create, Update }
    private sealed record TraceEvent(TraceEventKind Kind, string RunId, IDictionary<string, object?> Payload);

    private sealed class Span : ILlmSpan
    {
        private readonly LangSmithTracer _owner;
        private readonly DateTime _startedAt;
        private readonly ConcurrentDictionary<string, object> _tags = new();
        private int _finished;

        public Span(LangSmithTracer owner, string runId, DateTime startedAt)
        {
            _owner = owner;
            _startedAt = startedAt;
            RunId = runId;
        }

        public string RunId { get; }

        public void Tag(string key, object value) => _tags[key] = value;

        public void EndOk(object outputs) => Finish(outputs, error: null);
        public void EndError(Exception error) => Finish(outputs: null, error: error);

        public void Dispose()
        {
            // Implicit success if no explicit End was called. Matches "using var span" cleanup.
            if (Interlocked.Exchange(ref _finished, 1) == 0)
                _owner.Enqueue(BuildUpdate(outputs: null, error: null));
        }

        private void Finish(object? outputs, Exception? error)
        {
            if (Interlocked.Exchange(ref _finished, 1) != 0) return;
            _owner.Enqueue(BuildUpdate(outputs, error));
        }

        private TraceEvent BuildUpdate(object? outputs, Exception? error)
        {
            var payload = new Dictionary<string, object?>
            {
                ["end_time"] = DateTime.UtcNow,
                ["outputs"]  = outputs,
                ["error"]    = error?.ToString()
            };
            if (!_tags.IsEmpty)
                payload["extra"] = new Dictionary<string, object?> { ["tags"] = _tags };
            return new TraceEvent(TraceEventKind.Update, RunId, payload);
        }
    }
}
