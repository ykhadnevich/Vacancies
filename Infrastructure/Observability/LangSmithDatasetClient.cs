using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Observability;

/// <summary>
/// Synchronous LangSmith REST client for the **management API** — Datasets,
/// Examples, Sessions (Experiments), Runs, and Feedback.
///
/// **Intentionally separate** from <see cref="LangSmithTracer"/>:
/// <list type="bullet">
///   <item>The tracer is fire-and-forget (background channel, drops on failure)
///         — appropriate for high-volume hot-path telemetry.</item>
///   <item>This client is synchronous and surfaces HTTP errors as exceptions —
///         appropriate for one-shot evaluation jobs where a failed POST is a
///         hard error that must stop the pipeline.</item>
/// </list>
///
/// Auth: Service Key (<c>lsv2_sk_*</c>) via <c>x-api-key</c> header. Personal
/// Access Tokens (<c>lsv2_pt_*</c>) return 403 on POST /runs and POST /examples.
///
/// Used by:
/// <list type="bullet">
///   <item><c>LangSmithDatasetUploader</c> — uploads gold pairs as Examples.</item>
///   <item><c>LangSmithExperimentUploader</c> — creates Sessions and posts Runs
///         linked to Examples via <c>reference_example_id</c> + <c>session_id</c>.</item>
/// </list>
/// </summary>
public sealed class LangSmithDatasetClient
{
    private readonly HttpClient _http;
    private readonly ILogger<LangSmithDatasetClient> _logger;
    private readonly string _apiKey;
    private readonly string _endpoint;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public LangSmithDatasetClient(
        HttpClient http,
        IConfiguration configuration,
        ILogger<LangSmithDatasetClient> logger)
    {
        _http = http;
        _logger = logger;
        var section = configuration.GetSection("LangSmith");
        _apiKey = section["ApiKey"]
            ?? Environment.GetEnvironmentVariable("LANGSMITH_API_KEY")
            ?? Environment.GetEnvironmentVariable("LangSmith__ApiKey")
            ?? throw new InvalidOperationException(
                "LangSmith API key not configured. Set LangSmith:ApiKey or LANGSMITH_API_KEY env var. Must be Service Key (lsv2_sk_*).");
        _endpoint = section["Endpoint"] ?? "https://api.smith.langchain.com";

        _http.BaseAddress = new Uri(_endpoint + "/api/v1/");
        if (!_http.DefaultRequestHeaders.Contains("x-api-key"))
            _http.DefaultRequestHeaders.Add("x-api-key", _apiKey);
    }

    // ── Datasets ────────────────────────────────────────────────────────

    public async Task<DatasetRecord?> FindDatasetByNameAsync(string name, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"datasets?name={Uri.EscapeDataString(name)}", ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        await EnsureSuccessAsync(resp, $"GET datasets?name={name}", ct);
        var items = await resp.Content.ReadFromJsonAsync<List<DatasetRecord>>(JsonOpts, ct);
        return items?.FirstOrDefault(d => d.Name == name);
    }

    public async Task<DatasetRecord> CreateDatasetAsync(
        string name, string description, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["name"]        = name,
            ["description"] = description,
            ["data_type"]   = "kv"
        };
        using var resp = await _http.PostAsJsonAsync("datasets", body, JsonOpts, ct);
        await EnsureSuccessAsync(resp, $"POST datasets/{name}", ct);
        var created = await resp.Content.ReadFromJsonAsync<DatasetRecord>(JsonOpts, ct)
                     ?? throw new InvalidOperationException("Empty response from POST /datasets");
        _logger.LogInformation("Created LangSmith dataset {Name} = {Id}", name, created.Id);
        return created;
    }

    public async Task<DatasetRecord> EnsureDatasetAsync(
        string name, string description, CancellationToken ct = default)
    {
        var existing = await FindDatasetByNameAsync(name, ct);
        if (existing is not null)
        {
            _logger.LogInformation("Reusing existing LangSmith dataset {Name} = {Id}", name, existing.Id);
            return existing;
        }
        return await CreateDatasetAsync(name, description, ct);
    }

    // ── Examples ────────────────────────────────────────────────────────

    public async Task<List<ExampleRecord>> ListExamplesAsync(
        string datasetId, CancellationToken ct = default)
    {
        var all = new List<ExampleRecord>();
        var offset = 0;
        while (true)
        {
            // LangSmith GET /examples uses query param `dataset` (NOT `dataset_id`).
            // Confirmed against the OpenAPI spec at api.smith.langchain.com/openapi.json.
            // Passing `dataset_id` returns HTTP 400:
            //   "Either dataset_id or id is required when as_of is a tag."
            using var resp = await _http.GetAsync(
                $"examples?dataset={datasetId}&limit=100&offset={offset}", ct);
            await EnsureSuccessAsync(resp, $"GET examples (offset={offset})", ct);
            var batch = await resp.Content.ReadFromJsonAsync<List<ExampleRecord>>(JsonOpts, ct);
            if (batch is null || batch.Count == 0) break;
            all.AddRange(batch);
            offset += batch.Count;
            if (batch.Count < 100) break;
        }
        return all;
    }

    public async Task<ExampleRecord> CreateExampleAsync(
        string datasetId,
        object inputs,
        object outputs,
        Dictionary<string, object?> metadata,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["dataset_id"] = datasetId,
            ["inputs"]     = inputs,
            ["outputs"]    = outputs,
            ["metadata"]   = metadata
        };
        using var resp = await _http.PostAsJsonAsync("examples", body, JsonOpts, ct);
        await EnsureSuccessAsync(resp, "POST examples", ct);
        return await resp.Content.ReadFromJsonAsync<ExampleRecord>(JsonOpts, ct)
               ?? throw new InvalidOperationException("Empty response from POST /examples");
    }

    // ── Sessions (Experiments) ──────────────────────────────────────────

    public async Task<Guid> CreateSessionAsync(
        string name,
        string description,
        string referenceDatasetId,
        Dictionary<string, object?>? metadata = null,
        CancellationToken ct = default)
    {
        var sessionId = Guid.NewGuid();
        var body = new Dictionary<string, object?>
        {
            ["id"]                   = sessionId,
            ["name"]                 = name,
            ["description"]          = description,
            ["start_time"]           = DateTime.UtcNow,
            ["reference_dataset_id"] = referenceDatasetId,
            ["extra"]                = new Dictionary<string, object?>
            {
                ["metadata"] = metadata ?? new Dictionary<string, object?>()
            }
        };
        using var resp = await _http.PostAsJsonAsync("sessions", body, JsonOpts, ct);
        await EnsureSuccessAsync(resp, $"POST sessions/{name}", ct);
        _logger.LogInformation("Created LangSmith experiment session {Name} = {Id}", name, sessionId);
        return sessionId;
    }

    public async Task CloseSessionAsync(Guid sessionId, CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?> { ["end_time"] = DateTime.UtcNow };
        using var resp = await _http.PatchAsJsonAsync($"sessions/{sessionId}", body, JsonOpts, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var msg = await resp.Content.ReadAsStringAsync(ct);
            _logger.LogWarning("Session close HTTP {Status}: {Body}",
                (int)resp.StatusCode, Trim(msg, 300));
        }
    }

    // ── Runs ────────────────────────────────────────────────────────────

    /// <summary>
    /// Post a completed run (start + end + outputs in one shot) linked to an
    /// experiment session and a dataset example.
    /// </summary>
    public async Task<Guid> PostRunAsync(
        Guid sessionId,
        Guid referenceExampleId,
        string runName,
        string runType,
        object inputs,
        object outputs,
        DateTime startTime,
        DateTime endTime,
        Dictionary<string, object?>? metadata = null,
        CancellationToken ct = default)
    {
        var runId = Guid.NewGuid();
        var body = new Dictionary<string, object?>
        {
            ["id"]                   = runId,
            ["name"]                 = runName,
            ["run_type"]             = runType,
            ["start_time"]           = startTime,
            ["end_time"]             = endTime,
            ["session_id"]           = sessionId,
            ["reference_example_id"] = referenceExampleId,
            ["inputs"]               = inputs,
            ["outputs"]              = outputs,
            ["extra"]                = new Dictionary<string, object?>
            {
                ["metadata"] = metadata ?? new Dictionary<string, object?>()
            }
        };
        using var resp = await _http.PostAsJsonAsync("runs", body, JsonOpts, ct);
        await EnsureSuccessAsync(resp, "POST runs", ct);
        return runId;
    }

    // ── Feedback ────────────────────────────────────────────────────────

    public async Task PostFeedbackAsync(
        Guid runId,
        string key,
        double score,
        string? value = null,
        string? comment = null,
        CancellationToken ct = default)
    {
        var body = new Dictionary<string, object?>
        {
            ["run_id"]          = runId,
            ["key"]             = key,
            ["score"]           = score,
            ["value"]           = value,
            ["comment"]         = comment,
            ["feedback_source"] = new Dictionary<string, object?> { ["type"] = "api" }
        };
        using var resp = await _http.PostAsJsonAsync("feedback", body, JsonOpts, ct);
        await EnsureSuccessAsync(resp, $"POST feedback ({key})", ct);
    }

    // ── Helpers ─────────────────────────────────────────────────────────

    private static async Task EnsureSuccessAsync(
        HttpResponseMessage resp, string what, CancellationToken ct)
    {
        if (resp.IsSuccessStatusCode) return;
        var body = await resp.Content.ReadAsStringAsync(ct);
        throw new HttpRequestException(
            $"LangSmith {what} failed: HTTP {(int)resp.StatusCode} {resp.StatusCode}. Body: {Trim(body, 800)}");
    }

    private static string Trim(string s, int max) =>
        string.IsNullOrEmpty(s) ? string.Empty
        : (s.Length <= max ? s : s[..max] + "…");

    // ── DTOs (PascalCase fields; SnakeCaseLower naming policy maps to JSON) ────

    public sealed record DatasetRecord(
        string Id,
        string Name,
        string? Description);

    public sealed record ExampleRecord(
        string Id,
        string DatasetId,
        Dictionary<string, object>? Inputs = null,
        Dictionary<string, object>? Outputs = null,
        Dictionary<string, object>? Metadata = null);
}
