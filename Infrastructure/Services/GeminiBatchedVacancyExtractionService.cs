using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Common.Diagnostics;
using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.VacancyNormalization;
using Infrastructure.RelevancePipeline.V2.VacancyNormalization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;


public sealed class GeminiBatchedVacancyExtractionService : IBatchedVacancyExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IVacancyDomainRouter _router;
    private readonly IVacancyNormalizationModuleResolver _resolver;
    private readonly IVacancyNormalizationPostProcessor _postProcessor;
    private readonly ILogger<GeminiBatchedVacancyExtractionService> _logger;

    private const string Model   = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";


    private const int ChunkSize = 5;


    private const int ParallelChunks = 3;


    private const int MaxRawCharsPerVacancy = 12_000;

    public string Version =>
        $"gemini-vac-normalization-batched-{VacancyNormalizationPromptCore.Version}+" + Model;

    private readonly ILlmTracer _tracer;

    public GeminiBatchedVacancyExtractionService(
        HttpClient httpClient,
        IConfiguration configuration,
        IVacancyDomainRouter router,
        IVacancyNormalizationModuleResolver resolver,
        IVacancyNormalizationPostProcessor postProcessor,
        ILogger<GeminiBatchedVacancyExtractionService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _router = router;
        _resolver = resolver;
        _postProcessor = postProcessor;
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<IReadOnlyDictionary<Guid, VacancyExtractionResult>> ExtractBatchAsync(
        IReadOnlyList<BatchedVacancyExtractionRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
            return new Dictionary<Guid, VacancyExtractionResult>();


        var prepared = new List<PreparedVacancy>(requests.Count);
        foreach (var r in requests)
        {
            var truncated = r.VacancyRawText.Length > MaxRawCharsPerVacancy
                ? r.VacancyRawText[..MaxRawCharsPerVacancy]
                : r.VacancyRawText;
            var domain = _router.Detect(truncated).Domain;
            prepared.Add(new PreparedVacancy(r.VacancyId, truncated, domain));
        }


        var byDomain = prepared
            .GroupBy(p => p.Domain)
            .ToDictionary(g => g.Key, g => g.ToList());

        _logger.LogInformation(
            "Batched vacancy normalize: {Total} vacancies, {Domains} domain groups (sizes: {Sizes})",
            requests.Count, byDomain.Count,
            string.Join(", ", byDomain.Select(kv => $"{kv.Key}={kv.Value.Count}")));


        var chunks = new List<(VacancyDomain Domain, IReadOnlyList<PreparedVacancy> Items)>();
        foreach (var (domain, items) in byDomain)
        {
            for (int i = 0; i < items.Count; i += ChunkSize)
            {
                int take = Math.Min(ChunkSize, items.Count - i);
                chunks.Add((domain, items.GetRange(i, take)));
            }
        }

        using var sem = new SemaphoreSlim(ParallelChunks, ParallelChunks);
        var tasks = chunks.Select(async chunk =>
        {
            await sem.WaitAsync(ct);
            try { return await ProcessChunkAsync(chunk.Domain, chunk.Items, ct); }
            finally { sem.Release(); }
        });
        var partials = await Task.WhenAll(tasks);


        var result = new Dictionary<Guid, VacancyExtractionResult>(requests.Count);
        foreach (var part in partials)
            foreach (var kv in part)
                result[kv.Key] = kv.Value;


        foreach (var p in prepared)
            if (!result.ContainsKey(p.VacancyId))
                result[p.VacancyId] = EmptyResult();

        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, VacancyExtractionResult>> ProcessChunkAsync(
        VacancyDomain domain,
        IReadOnlyList<PreparedVacancy> chunk,
        CancellationToken ct)
    {
        var module = _resolver.For(domain);
        var slots = module.GetSlots();
        var compositeVersion =
            $"{VacancyNormalizationPromptCore.Version}+{module.Version}";

        var prompt = BuildPrompt(slots, chunk);
        using var span = _tracer.StartSpan(
            name: "vacancy_normalize_batched",
            runType: LlmRunType.LLM,
            inputs: new { domain = domain.ToString(), chunk_size = chunk.Count, model = Model, version = compositeVersion, prompt });

        try
        {

            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {


                    temperature      = 0.1,
                    topP             = 0.95,


                    maxOutputTokens  = 16384,
                    thinkingConfig   = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema   = BuildResponseSchema()
                }
            };


            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(28));

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var swCall = Stopwatch.StartNew();
            var resp = await _httpClient.PostAsJsonAsync(url, body, perCallCts.Token);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsStringAsync(perCallCts.Token);
            swCall.Stop();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            int inputTokens = 0, outputTokens = 0;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                inputTokens  = ReadIntOrZero(usage, "promptTokenCount");
                outputTokens = ReadIntOrZero(usage, "candidatesTokenCount");
            }
            CostBreakdown.Track("vacancy_normalize_batched", swCall.Elapsed.TotalMilliseconds,
                                inputTokens, outputTokens);

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            {
                _logger.LogError(
                    "Batched vacancy normalize chunk: no candidates returned ({Count} vacancies → empty)",
                    chunk.Count);
                return BuildEmptyChunkResult(chunk);
            }
            var first = cands[0];


            if (first.TryGetProperty("finishReason", out var fr)
                && fr.ValueKind == JsonValueKind.String
                && string.Equals(fr.GetString(), "MAX_TOKENS", StringComparison.Ordinal))
            {
                _logger.LogError(
                    "Batched vacancy normalize chunk: Gemini hit MAX_TOKENS " +
                    "({Count} vacancies, output_tokens={OutTokens}). Increase " +
                    "maxOutputTokens or drop ChunkSize. Chunk falling back to empty.",
                    chunk.Count, outputTokens);
                return BuildEmptyChunkResult(chunk);
            }

            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
                return BuildEmptyChunkResult(chunk);

            string text = string.Empty;
            foreach (var p in parts.EnumerateArray())
            {
                if (p.TryGetProperty("thought", out var th) && th.GetBoolean())
                    continue;
                if (p.TryGetProperty("text", out var t))
                {
                    text = t.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text)) break;
                }
            }
            if (string.IsNullOrWhiteSpace(text))
                return BuildEmptyChunkResult(chunk);

            text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();


            int perInTokens  = chunk.Count == 0 ? 0 : inputTokens  / chunk.Count;
            int perOutTokens = chunk.Count == 0 ? 0 : outputTokens / chunk.Count;

            var parsed = ParseBatchOutput(text, chunk, compositeVersion, perInTokens, perOutTokens);
            // Capture the raw Gemini output JSON so the LangSmith UI can
            // render the actual normalised vacancy fields. Tracing is
            // best-effort — never throw out of the success path.
            JsonElement? extractedJson = null;
            try
            {
                using var extractedDoc = JsonDocument.Parse(text);
                extractedJson = extractedDoc.RootElement.Clone();
            }
            catch (JsonException) { /* tracing best-effort */ }

            span.EndOk(new
            {
                domain        = domain.ToString(),
                chunk_size    = chunk.Count,
                input_tokens  = inputTokens,
                output_tokens = outputTokens,
                latency_ms    = swCall.Elapsed.TotalMilliseconds,
                model_version = compositeVersion,
                extracted     = extractedJson
            });
            return parsed;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            span.EndError(new TimeoutException("vacancy_normalize_batched timeout"));
            _logger.LogError("Batched vacancy normalize chunk timed out ({Domain}, {Count} vacancies)",
                             domain, chunk.Count);
            return BuildEmptyChunkResult(chunk);
        }
        catch (Exception ex)
        {
            span.EndError(ex);
            _logger.LogError(ex,
                "Batched vacancy normalize chunk failed ({Domain}, {Count} vacancies)",
                domain, chunk.Count);
            return BuildEmptyChunkResult(chunk);
        }
    }


    private IReadOnlyDictionary<Guid, VacancyExtractionResult> ParseBatchOutput(
        string json,
        IReadOnlyList<PreparedVacancy> chunk,
        string compositeVersion,
        int perInTokens,
        int perOutTokens)
    {
        var result = new Dictionary<Guid, VacancyExtractionResult>(chunk.Count);
        var modelVersion = $"gemini-vac-normalization-batched-{compositeVersion}";

        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            JsonElement vacanciesEl = default;
            bool shapeOk = root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("vacancies", out vacanciesEl)
                        && vacanciesEl.ValueKind == JsonValueKind.Array;
            if (!shapeOk)
            {
                _logger.LogError(
                    "Batched vacancy normalize: expected {{vacancies:[…]}} but got root={RootKind}, " +
                    "vacancies={VacanciesKind} — falling back chunk of {Count}",
                    root.ValueKind,
                    vacanciesEl.ValueKind == JsonValueKind.Undefined ? "missing" : vacanciesEl.ValueKind.ToString(),
                    chunk.Count);
                return BuildEmptyChunkResult(chunk);
            }

            foreach (var item in vacanciesEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("vacancy_idx", out var idxEl)
                    || idxEl.ValueKind != JsonValueKind.Number) continue;
                int idx = idxEl.GetInt32();
                if (idx < 0 || idx >= chunk.Count) continue;


                string rawStructured = StripVacancyIdx(item);
                if (string.IsNullOrWhiteSpace(rawStructured)) continue;

                var prepared = chunk[idx];
                string processed;
                try
                {
                    processed = _postProcessor.Process(rawStructured, prepared.TruncatedText);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex,
                        "Batched vacancy normalize: post-processor failed for idx={Idx} vacancy={VacancyId}",
                        idx, prepared.VacancyId);
                    processed = rawStructured;
                }

                result[prepared.VacancyId] = new VacancyExtractionResult(
                    Json:         processed,
                    ModelVersion: modelVersion,
                    InputTokens:  perInTokens,
                    OutputTokens: perOutTokens);
            }


            if (result.Count < chunk.Count)
            {
                var missing = new List<string>();
                for (int i = 0; i < chunk.Count; i++)
                    if (!result.ContainsKey(chunk[i].VacancyId))
                    {
                        missing.Add($"idx={i}/vacancy={chunk[i].VacancyId}");
                        result[chunk[i].VacancyId] = EmptyResult();
                    }
                _logger.LogWarning(
                    "Batched vacancy normalize: {Got}/{Expected} parsed. Missing → empty: {Missing}",
                    chunk.Count - missing.Count, chunk.Count, string.Join("; ", missing));
            }
        }
        catch (JsonException jx)
        {
            _logger.LogError(jx,
                "Batched vacancy normalize: JSON parse failed (first 200 chars: {Snippet})",
                json.Length > 200 ? json[..200] : json);
            return BuildEmptyChunkResult(chunk);
        }
        return result;
    }

    private static string StripVacancyIdx(JsonElement item)
    {
        try
        {
            using var ms = new MemoryStream();
            using (var writer = new Utf8JsonWriter(ms))
            {
                writer.WriteStartObject();
                foreach (var prop in item.EnumerateObject())
                {
                    if (prop.NameEquals("vacancy_idx")) continue;
                    prop.WriteTo(writer);
                }
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(ms.ToArray());
        }
        catch
        {
            return string.Empty;
        }
    }

    private static IReadOnlyDictionary<Guid, VacancyExtractionResult> BuildEmptyChunkResult(
        IReadOnlyList<PreparedVacancy> chunk)
    {
        var dict = new Dictionary<Guid, VacancyExtractionResult>(chunk.Count);
        foreach (var p in chunk)
            dict[p.VacancyId] = EmptyResult();
        return dict;
    }

    private static VacancyExtractionResult EmptyResult() =>
        new(Json: string.Empty, ModelVersion: string.Empty, InputTokens: 0, OutputTokens: 0);

    private static int ReadIntOrZero(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32() : 0;


    private static object BuildResponseSchema()
    {


        var stringObj = new Dictionary<string, object> { ["type"] = "STRING" };
        Dictionary<string, object> StringEnum(params string[] values) => new()
        {
            ["type"] = "STRING",
            ["enum"] = values
        };
        Dictionary<string, object> StringArray() => new()
        {
            ["type"] = "ARRAY",
            ["items"] = stringObj
        };
        var bilingualText = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["en"] = stringObj,
                ["uk"] = stringObj
            },
            ["required"] = new[] { "en", "uk" }
        };
        var locationObj = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["city_en"] = new Dictionary<string, object> { ["type"] = "STRING", ["nullable"] = true },
                ["city_uk"] = new Dictionary<string, object> { ["type"] = "STRING", ["nullable"] = true },
                ["remote"]  = new Dictionary<string, object> { ["type"] = "BOOLEAN" },
                ["hybrid"]  = new Dictionary<string, object> { ["type"] = "BOOLEAN" }
            },
            ["required"] = new[] { "remote", "hybrid" }
        };
        var vacancyItem = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["vacancy_idx"]          = new Dictionary<string, object> { ["type"] = "INTEGER" },
                ["source_language"]      = StringEnum("uk", "en", "mixed", "unknown"),
                ["role_title"]           = bilingualText,
                ["role_title_raw"]       = stringObj,
                ["seniority_required"]   = StringEnum("junior", "middle", "senior", "lead", "intern", "not_specified"),
                ["must_have_skills"]     = StringArray(),
                ["nice_to_have_skills"]  = StringArray(),
                ["min_years_experience"] = new Dictionary<string, object> { ["type"] = "INTEGER", ["nullable"] = true },
                ["education_required"]   = StringEnum("none", "bachelor", "master", "phd", "not_specified"),
                ["english_required"]     = StringEnum("A1", "A2", "B1", "B2", "C1", "C2", "native", "not_specified"),
                ["location"]             = locationObj,
                ["domain_context"]       = bilingualText,
                ["anti_requirements"]    = StringArray(),
                ["confidence"]           = new Dictionary<string, object>
                {
                    ["type"]        = "NUMBER",
                    ["description"] = "Self-reported certainty about this extraction in [0.0, 1.0]. " +
                                      "1.0 = detailed vacancy with explicit fields. 0.2 = near-empty input."
                }
            },
            ["required"] = new[]
            {
                "vacancy_idx",
                "source_language", "role_title", "role_title_raw",
                "seniority_required", "must_have_skills", "nice_to_have_skills",
                "education_required", "english_required",
                "location", "domain_context", "anti_requirements",
                "confidence"
            }
        };

        return new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["vacancies"] = new Dictionary<string, object>
                {
                    ["type"]  = "ARRAY",
                    ["items"] = vacancyItem
                }
            },
            ["required"] = new[] { "vacancies" }
        };
    }

    private static string BuildPrompt(
        VacancyNormalizationSlots slots,
        IReadOnlyList<PreparedVacancy> chunk)
    {
        var sb = new StringBuilder(16384);
        sb.AppendLine("You are a vacancy parsing expert. Below are N vacancies from the SAME industry");
        sb.AppendLine("domain. For EACH vacancy, extract a structured job posting analysis following");
        sb.AppendLine("the procedure in section A–F. Each vacancy is independent — do NOT let one");
        sb.AppendLine("vacancy's evidence influence another's analysis.");
        sb.AppendLine();
        sb.AppendLine("Output a single JSON object of shape {\"vacancies\":[ … ]} where the array");
        sb.AppendLine("contains ONE object per input vacancy. Each object MUST include vacancy_idx");
        sb.AppendLine("(0-based int matching the INPUT order) plus the full VacancyAnalysis schema.");
        sb.AppendLine();
        sb.AppendLine("=== INSTRUCTIONS (apply to every vacancy independently) ===");
        sb.AppendLine();


        sb.Append(VacancyNormalizationPromptCore.BuildInstructionsBody(slots));
        sb.AppendLine();
        sb.AppendLine();
        sb.AppendLine("=== INPUT VACANCIES ===");
        sb.AppendLine();
        for (int i = 0; i < chunk.Count; i++)
        {
            sb.AppendLine($"--- vacancy_idx {i} ---");
            sb.AppendLine(chunk[i].TruncatedText);
            sb.AppendLine();
        }
        sb.AppendLine("Return the {\"vacancies\":[…]} object now. ONLY the JSON, no prose, no markdown fences.");
        return sb.ToString();
    }


    private sealed record PreparedVacancy(
        Guid VacancyId,
        string TruncatedText,
        VacancyDomain Domain);
}
