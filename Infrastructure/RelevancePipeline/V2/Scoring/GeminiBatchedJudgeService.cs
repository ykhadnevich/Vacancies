using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Common.Diagnostics;
using Application.Common.Interfaces;
using Domain.Scoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public sealed class GeminiBatchedJudgeService : IBatchedJudgeService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiBatchedJudgeService> _logger;

    private const string Model   = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";


    private const int ChunkSize = 5;


    private const int ParallelChunks = 3;


    public string Version => "batched_judge_v2+" + JudgePromptCore.BodyVersion + "+" + Model;

    private readonly ILlmTracer _tracer;

    public GeminiBatchedJudgeService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiBatchedJudgeService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<IReadOnlyDictionary<Guid, BatchedJudgeResult>> JudgeBatchAsync(
        string cvSummaryJson,
        IReadOnlyList<BatchedJudgeRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0)
            return new Dictionary<Guid, BatchedJudgeResult>();


        var chunks = new List<IReadOnlyList<BatchedJudgeRequest>>();
        for (int i = 0; i < requests.Count; i += ChunkSize)
        {
            int take = Math.Min(ChunkSize, requests.Count - i);
            var slice = new BatchedJudgeRequest[take];
            for (int j = 0; j < take; j++) slice[j] = requests[i + j];
            chunks.Add(slice);
        }

        _logger.LogInformation(
            "Batched judge: {Total} pairs → {Chunks} chunks of ≤{ChunkSize} (parallelism={Par})",
            requests.Count, chunks.Count, ChunkSize, ParallelChunks);

        using var sem = new SemaphoreSlim(ParallelChunks, ParallelChunks);
        var tasks = chunks.Select(async chunk =>
        {
            await sem.WaitAsync(ct);
            try { return await JudgeSingleChunkAsync(cvSummaryJson, chunk, ct); }
            finally { sem.Release(); }
        });
        var partials = await Task.WhenAll(tasks);

        var result = new Dictionary<Guid, BatchedJudgeResult>();
        foreach (var part in partials)
            foreach (var kv in part)
                result[kv.Key] = kv.Value;
        return result;
    }

    private async Task<IReadOnlyDictionary<Guid, BatchedJudgeResult>> JudgeSingleChunkAsync(
        string cvSummaryJson,
        IReadOnlyList<BatchedJudgeRequest> chunk,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(cvSummaryJson, chunk);
        using var span = _tracer.StartSpan(
            name: "judge_batched",
            runType: LlmRunType.LLM,
            inputs: new { chunk_size = chunk.Count, model = Model, version = Version, prompt });

        try
        {
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature      = 0.1,
                    topP             = 0.95,


                    maxOutputTokens  = 4096,
                    thinkingConfig   = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema   = BuildResponseSchema()
                }
            };


            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(18));

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
                if (usage.TryGetProperty("promptTokenCount", out var pIn)
                    && pIn.ValueKind == JsonValueKind.Number)
                    inputTokens = pIn.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var pOut)
                    && pOut.ValueKind == JsonValueKind.Number)
                    outputTokens = pOut.GetInt32();
            }
            CostBreakdown.Track("judge_batched", swCall.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            {
                _logger.LogError(
                    "Batched judge chunk: no candidates returned ({Count} pairs → empty)",
                    chunk.Count);
                return BuildFallbackDict(chunk, "no_candidates");
            }
            var first = cands[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
                return BuildFallbackDict(chunk, "no_content_parts");

            string text = string.Empty;
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? string.Empty; break; }
            if (string.IsNullOrWhiteSpace(text))
                return BuildFallbackDict(chunk, "empty_text");

            text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
            var parsed = ParseBatchOutput(text, chunk);
            // Raw Gemini output JSON so each pair's final_score + confidence
            // is visible in LangSmith UI. Tracing is best-effort.
            JsonElement? extractedJson = null;
            try
            {
                using var extractedDoc = JsonDocument.Parse(text);
                extractedJson = extractedDoc.RootElement.Clone();
            }
            catch (JsonException) { /* tracing best-effort */ }

            span.EndOk(new
            {
                chunk_size    = chunk.Count,
                parsed_pairs  = parsed.Count,
                input_tokens  = inputTokens,
                output_tokens = outputTokens,
                latency_ms    = swCall.Elapsed.TotalMilliseconds,
                extracted     = extractedJson
            });
            return parsed;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            span.EndError(new TimeoutException("judge_batched timeout"));
            _logger.LogError("Batched judge chunk timed out — {Count} pairs dropped to linear", chunk.Count);
            return BuildFallbackDict(chunk, "timeout");
        }
        catch (Exception ex)
        {
            span.EndError(ex);
            _logger.LogError(ex, "Batched judge chunk failed — {Count} pairs dropped to linear", chunk.Count);
            return BuildFallbackDict(chunk, "http_or_parse_error");
        }
    }

    private IReadOnlyDictionary<Guid, BatchedJudgeResult> ParseBatchOutput(
        string json, IReadOnlyList<BatchedJudgeRequest> chunk)
    {
        var result = new Dictionary<Guid, BatchedJudgeResult>();
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;


            JsonElement pairsEl = default;
            bool shapeOk = root.ValueKind == JsonValueKind.Object
                        && root.TryGetProperty("pairs", out pairsEl)
                        && pairsEl.ValueKind == JsonValueKind.Array;
            if (!shapeOk)
            {
                _logger.LogWarning(
                    "Batched judge: expected {{pairs:[...]}} but got root={RootKind}, " +
                    "pairs={PairsKind} — falling back chunk of {Count}",
                    root.ValueKind,
                    pairsEl.ValueKind == JsonValueKind.Undefined ? "missing" : pairsEl.ValueKind.ToString(),
                    chunk.Count);
                return BuildFallbackDict(chunk, "shape_mismatch");
            }

            int rejected = 0;
            foreach (var item in pairsEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (!item.TryGetProperty("pair_idx", out var idxEl)
                    || idxEl.ValueKind != JsonValueKind.Number) continue;
                int idx = idxEl.GetInt32();
                if (idx < 0 || idx >= chunk.Count) continue;

                if (!item.TryGetProperty("final_score", out var scoreEl)
                    || scoreEl.ValueKind != JsonValueKind.Number)
                { rejected++; continue; }

                double score = scoreEl.GetDouble();
                if (double.IsNaN(score) || double.IsInfinity(score))
                { rejected++; continue; }

                score = Math.Clamp(score, 0.0, 1.0);

                result[chunk[idx].VacancyId] = new BatchedJudgeResult(
                    FinalScore: score,
                    FallbackUsed: false,
                    FailureReason: null);
            }

            if (result.Count < chunk.Count)
            {
                var dropped = new List<string>();
                for (int i = 0; i < chunk.Count; i++)
                {
                    if (!result.ContainsKey(chunk[i].VacancyId))
                    {
                        dropped.Add($"idx={i}/vacancy={chunk[i].VacancyId}");
                        result[chunk[i].VacancyId] = new BatchedJudgeResult(
                            FinalScore: chunk[i].LinearScore,
                            FallbackUsed: true,
                            FailureReason: "missing_or_malformed_pair");
                    }
                }
                _logger.LogWarning(
                    "Batched judge: {Returned}/{Expected} pairs parsed " +
                    "({Rejected} rejected). Missing → linear fallback: {Missing}",
                    chunk.Count - dropped.Count, chunk.Count, rejected,
                    string.Join("; ", dropped));
            }
        }
        catch (JsonException jx)
        {
            _logger.LogWarning(jx,
                "Batched judge JSON parse failed for chunk of {Count} (first 200 chars: {Snippet})",
                chunk.Count, json.Length > 200 ? json[..200] : json);
            return BuildFallbackDict(chunk, "json_parse_error");
        }
        return result;
    }

    private static IReadOnlyDictionary<Guid, BatchedJudgeResult> BuildFallbackDict(
        IReadOnlyList<BatchedJudgeRequest> chunk, string reason)
    {
        var dict = new Dictionary<Guid, BatchedJudgeResult>(chunk.Count);
        foreach (var r in chunk)
            dict[r.VacancyId] = new BatchedJudgeResult(
                FinalScore: r.LinearScore,
                FallbackUsed: true,
                FailureReason: reason);
        return dict;
    }

    private static object BuildResponseSchema() => new
    {
        type = "OBJECT",
        properties = new
        {
            pairs = new
            {
                type = "ARRAY",
                items = new
                {
                    type = "OBJECT",
                    properties = new
                    {
                        pair_idx    = new { type = "INTEGER" },
                        final_score = new { type = "NUMBER", description = "Composite score in [0,1]" },
                        confidence  = new
                        {
                            type        = "NUMBER",
                            description = "Self-reported certainty about this score in [0.0, 1.0]. " +
                                          "1.0 = both CV and vacancy detailed, overlap unambiguous. " +
                                          "0.4 = significant missing info — flag for human review."
                        }
                    },
                    required = new[] { "pair_idx", "final_score", "confidence" }
                }
            }
        },
        required = new[] { "pairs" }
    };


    private static string BuildPrompt(
        string cvSummaryJson,
        IReadOnlyList<BatchedJudgeRequest> chunk)
    {
        var sb = new StringBuilder(16384);
        sb.AppendLine("You evaluate how well candidates match job vacancies.");
        sb.AppendLine("For EACH (CV, vacancy) pair below produce ONE composite score.");
        sb.AppendLine("Score each pair INDEPENDENTLY — one pair's evidence MUST NOT influence");
        sb.AppendLine("another pair's score.");
        sb.AppendLine();
        sb.AppendLine("=== CV (shared by all pairs in this batch) ===");
        sb.AppendLine();
        sb.AppendLine(cvSummaryJson);
        sb.AppendLine();


        RoleFamily family = RoleFamily.Other;
        try
        {
            using var cvDoc = JsonDocument.Parse(cvSummaryJson);
            family = RoleFamilyDetector.Detect(cvDoc.RootElement);
        }
        catch (JsonException) {  }


        sb.Append(JudgePromptCore.Build(family));

        sb.AppendLine();
        sb.AppendLine("=== OUTPUT FORMAT ===");
        sb.AppendLine();
        sb.AppendLine("Return a COMPACT single-line JSON object of shape {\"pairs\":[...]}.");
        sb.AppendLine("One object per input pair, in INPUT order:");
        sb.AppendLine("  {\"pair_idx\":<int>, \"final_score\":<number in [0,1]>, \"confidence\":<number in [0,1]>}");
        sb.AppendLine();
        sb.AppendLine("Example (multi-line here for readability, RETURN COMPACT):");
        sb.AppendLine("{\"pairs\":[");
        sb.AppendLine("  {\"pair_idx\":0,\"final_score\":0.85,\"confidence\":0.9},");
        sb.AppendLine("  {\"pair_idx\":1,\"final_score\":0.42,\"confidence\":0.6}");
        sb.AppendLine("]}");
        sb.AppendLine();
        sb.AppendLine("=== INPUT — pairs to score ===");
        sb.AppendLine();
        for (int i = 0; i < chunk.Count; i++)
        {
            var r = chunk[i];
            sb.AppendLine($"--- pair_idx {i} ---");
            sb.AppendLine("VACANCY (normalized JSON):");
            sb.AppendLine(r.VacancyAnalysisJson);
            sb.AppendLine();
            sb.AppendLine("DETERMINISTIC SUB-SCORES (each in [0,1]):");
            sb.AppendLine($"  skill_match       = {r.SubScores.SkillMatch:F3}");
            sb.AppendLine($"  seniority_match   = {r.SubScores.SeniorityMatch:F3}");
            sb.AppendLine($"  experience_match  = {r.SubScores.ExperienceMatch:F3}");
            sb.AppendLine($"  language_match    = {r.SubScores.LanguageMatch:F3}");
            sb.AppendLine($"  education_match   = {r.SubScores.EducationMatch:F3}");
            sb.AppendLine($"  role_intent_match = {r.SubScores.RoleIntentMatch:F3}");
            sb.AppendLine($"  domain_alignment  = {r.SubScores.DomainAlignment:F3}");
            sb.AppendLine();
            sb.AppendLine("EVIDENCE:");
            sb.AppendLine($"  matched_skills:        {(r.Evidence.MatchedSkills.Count == 0 ? "(none)" : string.Join(", ", r.Evidence.MatchedSkills))}");
            sb.AppendLine($"  missing_must_haves:    {(r.Evidence.MissingMustHaves.Count == 0 ? "(none)" : string.Join(", ", r.Evidence.MissingMustHaves))}");
            sb.AppendLine($"  triggered_anti_flags:  {(r.Evidence.TriggeredAntiFlags.Count == 0 ? "(none)" : string.Join(", ", r.Evidence.TriggeredAntiFlags))}");
            sb.AppendLine();
            sb.AppendLine("LINEAR-FORMULA INITIAL ANCHOR:");
            sb.AppendLine($"  initial_score   = {r.LinearScore:F3}");
            sb.AppendLine($"  initial_verdict = {r.LinearVerdict}");
            sb.AppendLine();
        }

        sb.AppendLine("Return the {\"pairs\":[…]} object now. ONLY the JSON, no prose, no markdown fences.");
        return sb.ToString();
    }
}
