using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Common.Diagnostics;
using Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.SkillExpansion;


public sealed class GeminiBatchSkillExpander : IBatchSkillExpander
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiBatchSkillExpander> _logger;
    private readonly ILlmTracer _tracer;

    private const string Model   = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";


    public string Version => "batch_v3+" + Model;


    private const int ChunkSize = 80;


    private const int ChunkParallelism = 3;

    public GeminiBatchSkillExpander(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiBatchSkillExpander> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<IReadOnlyDictionary<string, string>> ExpandBatchAsync(
        IReadOnlyList<string> skills,
        string? roleFamilyHint,
        CancellationToken ct = default)
    {
        if (skills.Count == 0)
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);


        var chunks = new List<IReadOnlyList<string>>();
        for (int i = 0; i < skills.Count; i += ChunkSize)
        {
            int take = Math.Min(ChunkSize, skills.Count - i);
            var chunk = new string[take];
            for (int j = 0; j < take; j++) chunk[j] = skills[i + j];
            chunks.Add(chunk);
        }

        _logger.LogInformation(
            "Batch skill expansion: {Total} skills → {Chunks} chunks of ≤{ChunkSize} (parallelism={Par})",
            skills.Count, chunks.Count, ChunkSize, ChunkParallelism);

        using var sem = new SemaphoreSlim(ChunkParallelism, ChunkParallelism);
        var tasks = chunks.Select(async chunk =>
        {
            await sem.WaitAsync(ct);
            try { return await ExpandSingleChunkAsync(chunk, roleFamilyHint, ct); }
            finally { sem.Release(); }
        });
        var partials = await Task.WhenAll(tasks);


        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var part in partials)
            foreach (var kv in part)
                if (!result.ContainsKey(kv.Key)) result[kv.Key] = kv.Value;
        return result;
    }


    private async Task<IReadOnlyDictionary<string, string>> ExpandSingleChunkAsync(
        IReadOnlyList<string> chunk,
        string? roleFamilyHint,
        CancellationToken ct)
    {
        var prompt = BuildPrompt(chunk, roleFamilyHint);
        using var span = _tracer.StartSpan(
            name: "skill_expansion_batch",
            runType: LlmRunType.LLM,
            inputs: new { prompt, model = Model, version = Version, chunk_size = chunk.Count, role_family_hint = roleFamilyHint });
        try
        {
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature      = 0.0,
                    topP             = 0.95,
                    maxOutputTokens  = 8192,
                    thinkingConfig   = new { thinkingBudget = 0 },
                    responseMimeType = "application/json"


                }
            };


            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(60));

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var swCall = Stopwatch.StartNew();
            var resp = await _httpClient.PostAsJsonAsync(url, body, perCallCts.Token);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsStringAsync(perCallCts.Token);
            swCall.Stop();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            int inputTokens = 0, outputTokens = 0;
            string? finishReason = null;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount", out var pIn)
                    && pIn.ValueKind == JsonValueKind.Number)
                    inputTokens = pIn.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var pOut)
                    && pOut.ValueKind == JsonValueKind.Number)
                    outputTokens = pOut.GetInt32();
            }


            CostBreakdown.Track("skill_expansion", swCall.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            {
                _logger.LogWarning(
                    "Batch skill expansion chunk: no candidates returned ({Count} skills → identity)",
                    chunk.Count);
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = true, reason = "no_candidates" });
                return IdentityMap(chunk);
            }
            var first = cands[0];
            if (first.TryGetProperty("finishReason", out var fr) && fr.ValueKind == JsonValueKind.String)
                finishReason = fr.GetString();
            if (string.Equals(finishReason, "MAX_TOKENS", StringComparison.OrdinalIgnoreCase))
            {


                _logger.LogWarning(
                    "Batch skill expansion chunk hit MAX_TOKENS ({Count} skills, out={Out}) — output may be truncated",
                    chunk.Count, outputTokens);
            }
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
            {
                _logger.LogWarning(
                    "Batch skill expansion chunk: empty content/parts ({Count} skills → identity)",
                    chunk.Count);
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = true, reason = "no_parts", finish_reason = finishReason });
                return IdentityMap(chunk);
            }

            string text = string.Empty;
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? string.Empty; break; }
            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "Batch skill expansion chunk: empty text part ({Count} skills → identity)",
                    chunk.Count);
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = true, reason = "empty_text", finish_reason = finishReason });
                return IdentityMap(chunk);
            }

            text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
            var parsed = ParseAndPatch(text, chunk, _logger);
            JsonElement? extractedJson = null;
            try
            {
                using var extractedDoc = JsonDocument.Parse(text);
                extractedJson = extractedDoc.RootElement.Clone();
            }
            catch (JsonException) { /* tracing best-effort */ }

            span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = false, parsed_keys = parsed.Count, finish_reason = finishReason, skills_in = chunk.Count, extracted = extractedJson });
            return parsed;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Batch skill expansion chunk timed out — identity fallback for {Count} skills",
                chunk.Count);
            span.EndError(new TimeoutException("skill_expansion_batch timeout"));
            return IdentityMap(chunk);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Batch skill expansion chunk failed — identity fallback for {Count} skills",
                chunk.Count);
            span.EndError(ex);
            return IdentityMap(chunk);
        }
    }


    private static IReadOnlyDictionary<string, string> ParseAndPatch(
        string llmJson, IReadOnlyList<string> originalSkills, ILogger logger)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        Dictionary<string, JsonElement>? produced = null;
        try
        {
            using var doc = JsonDocument.Parse(llmJson);
            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                produced = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
                foreach (var prop in doc.RootElement.EnumerateObject())
                    produced[prop.Name] = prop.Value.Clone();
            }
        }
        catch (JsonException jx)
        {


            logger.LogWarning(jx,
                "Batch expansion ParseAndPatch: JSON parse failed — identity fallback for {Count} skills " +
                "(likely maxOutputTokens truncation; first 200 chars: {Snippet})",
                originalSkills.Count,
                llmJson.Length > 200 ? llmJson[..200] : llmJson);
            return IdentityMap(originalSkills);
        }

        foreach (var skill in originalSkills)
        {
            if (produced is null
                || !produced.TryGetValue(skill, out var arrEl)
                || arrEl.ValueKind != JsonValueKind.Array
                || arrEl.GetArrayLength() == 0)
            {
                result[skill] = IdentityJson(skill);
                continue;
            }


            bool hasSelf = false;
            foreach (var item in arrEl.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.Object) continue;
                if (item.TryGetProperty("term", out var tEl)
                    && tEl.ValueKind == JsonValueKind.String
                    && string.Equals(tEl.GetString(), skill, StringComparison.OrdinalIgnoreCase))
                {
                    hasSelf = true;
                    break;
                }
            }

            if (hasSelf)
            {
                result[skill] = arrEl.GetRawText();
            }
            else
            {
                var sb = new StringBuilder(arrEl.GetRawText().Length + 64);
                sb.Append("[{\"term\":");
                sb.Append(JsonSerializer.Serialize(skill));
                sb.Append(",\"confidence\":1.0}");
                foreach (var item in arrEl.EnumerateArray())
                {
                    sb.Append(',');
                    sb.Append(item.GetRawText());
                }
                sb.Append(']');
                result[skill] = sb.ToString();
            }
        }
        return result;
    }


    private static string IdentityJson(string skill)
    {
        var sb = new StringBuilder(64);
        sb.Append("[{\"term\":");
        sb.Append(JsonSerializer.Serialize(skill));
        sb.Append(",\"confidence\":1.0}]");
        return sb.ToString();
    }


    private static IReadOnlyDictionary<string, string> IdentityMap(IReadOnlyList<string> skills)
    {
        var d = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in skills) d[s] = IdentityJson(s);
        return d;
    }

    private static string BuildPrompt(IReadOnlyList<string> skills, string? roleFamilyHint)
    {


        var sb = new StringBuilder(4096);
        sb.AppendLine("You are a skill ontology expander for a job-matching system.");
        sb.AppendLine();
        sb.AppendLine("Input: a deduplicated list of skill names collected across one user's CV");
        sb.AppendLine("and the vacancies currently being scored. Skills come from different sources");
        sb.AppendLine("and SHOULD be expanded independently — do NOT condition one skill's expansion");
        sb.AppendLine("on the presence of its neighbors.");
        sb.AppendLine();
        sb.AppendLine("Output: ONE JSON object whose keys are the EXACT input skill names");
        sb.AppendLine("(verbatim, preserving casing and punctuation) and whose values are arrays of");
        sb.AppendLine("{term, confidence} expansion entries.");
        sb.AppendLine();
        sb.AppendLine("=== RULES ===");
        sb.AppendLine();
        sb.AppendLine("1. ADDITIVE — the original skill must always appear in its own array as the");
        sb.AppendLine("   FIRST entry with confidence 1.0. Never replace the original; only add");
        sb.AppendLine("   expansion terms beside it.");
        sb.AppendLine();
        sb.AppendLine("2. EXPANSION TYPES (reasoning hint — pick which terms to add):");
        sb.AppendLine("     synonym          — different surface form, same concept");
        sb.AppendLine("                        (\"A/B testing\" ↔ \"experimentation\", \"split testing\")");
        sb.AppendLine("     language_variant — translation / transliteration of the same term");
        sb.AppendLine("                        (\"A/B testing\" → \"А/Б тестування\")");
        sb.AppendLine("     parent           — broader concept the original belongs to");
        sb.AppendLine("                        (\"Cohort analysis\" → \"product analytics\",");
        sb.AppendLine("                         \"behavioral analytics\")");
        sb.AppendLine("     brand_generic    — generic category a brand product fulfills");
        sb.AppendLine("                        (\"Stripe Billing\" → \"billing\", \"payment processing\")");
        sb.AppendLine();
        sb.AppendLine("3. CONFIDENCE — a number in (0, 1]:");
        sb.AppendLine("     1.0      = self (always first)");
        sb.AppendLine("     0.9–1.0  = synonym or language_variant");
        sb.AppendLine("     0.7–0.9  = brand_generic, well-known parent concept");
        sb.AppendLine("     0.5–0.7  = weaker parent or domain-specific synonym");
        sb.AppendLine("     never below 0.3");
        sb.AppendLine();
        sb.AppendLine("4. AGGRESSIVE COVERAGE — expand each skill broadly within its concept space.");
        sb.AppendLine("   Aim for 2–4 expansion terms PLUS the self entry (3–5 entries total per skill).");
        sb.AppendLine("   The cross-vocabulary matcher downstream relies on these terms — under-");
        sb.AppendLine("   expansion silently drops matches between CV \"Cohort analysis\" and vacancy");
        sb.AppendLine("   \"product analytics\".");
        sb.AppendLine();
        sb.AppendLine("5. PRESERVE SPECIFICITY — never drop domain detail. Always include the");
        sb.AppendLine("   specific term alongside its parent.");
        sb.AppendLine("   BAD : \"Bayesian A/B testing with CUPED\" → just \"A/B testing\".");
        sb.AppendLine("   GOOD: include \"CUPED\", \"variance reduction\", \"Bayesian inference\"");
        sb.AppendLine("         alongside the original.");
        sb.AppendLine();
        sb.AppendLine("6. CROSS-LANGUAGE — if a skill is in Ukrainian/Russian, INCLUDE the English");
        sb.AppendLine("   equivalent as a synonym (and vice-versa). \"A/B-тестування\" → \"A/B testing\".");
        sb.AppendLine();
        sb.AppendLine("7. NEVER drop input keys — every input MUST appear in the output. If you have");
        sb.AppendLine("   nothing meaningful to add, output the self entry only — but every input");
        sb.AppendLine("   key MUST be present.");
        sb.AppendLine();
        if (!string.IsNullOrWhiteSpace(roleFamilyHint))
        {
            sb.Append("ROLE-FAMILY CONTEXT (use to disambiguate, NOT to filter): ");
            sb.AppendLine(roleFamilyHint);
            sb.AppendLine("When a skill has multiple interpretations across role families, pick the one");
            sb.AppendLine("consistent with this role family. Do NOT drop skills that don't fit this role —");
            sb.AppendLine("the CV side has no such hint.");
            sb.AppendLine();
        }
        sb.AppendLine("=== EXAMPLE OUTPUT (target shape — keep this compact) ===");
        sb.AppendLine();
        sb.AppendLine("{");
        sb.AppendLine("  \"Cohort analysis\": [");
        sb.AppendLine("    {\"term\":\"Cohort analysis\",\"confidence\":1.0},");
        sb.AppendLine("    {\"term\":\"retention analytics\",\"confidence\":0.9},");
        sb.AppendLine("    {\"term\":\"product analytics\",\"confidence\":0.8},");
        sb.AppendLine("    {\"term\":\"behavioral analytics\",\"confidence\":0.7}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"Stripe Billing\": [");
        sb.AppendLine("    {\"term\":\"Stripe Billing\",\"confidence\":1.0},");
        sb.AppendLine("    {\"term\":\"billing\",\"confidence\":0.9},");
        sb.AppendLine("    {\"term\":\"payment processing\",\"confidence\":0.85},");
        sb.AppendLine("    {\"term\":\"subscription billing\",\"confidence\":0.7}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"A/B-тестування\": [");
        sb.AppendLine("    {\"term\":\"A/B-тестування\",\"confidence\":1.0},");
        sb.AppendLine("    {\"term\":\"A/B testing\",\"confidence\":1.0},");
        sb.AppendLine("    {\"term\":\"experimentation\",\"confidence\":0.85},");
        sb.AppendLine("    {\"term\":\"split testing\",\"confidence\":0.8}");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("=== INPUT SKILLS ===");
        sb.AppendLine();
        for (int i = 0; i < skills.Count; i++)
        {
            sb.Append("- ");
            sb.AppendLine(skills[i]);
        }
        sb.AppendLine();
        sb.AppendLine("=== CRITICAL OUTPUT FORMAT — TOKEN BUDGET ===");
        sb.AppendLine();
        sb.AppendLine("Return COMPACT JSON on a SINGLE line with NO whitespace between tokens:");
        sb.AppendLine("  - NO line breaks between properties or array elements");
        sb.AppendLine("  - NO leading spaces / indentation");
        sb.AppendLine("  - NO spaces around colons or commas");
        sb.AppendLine();
        sb.AppendLine("Compact form (REQUIRED):");
        sb.AppendLine("{\"Cohort analysis\":[{\"term\":\"Cohort analysis\",\"confidence\":1.0},{\"term\":\"retention analytics\",\"confidence\":0.9}],\"SQL\":[{\"term\":\"SQL\",\"confidence\":1.0}]}");
        sb.AppendLine();
        sb.AppendLine("Pretty-printed form (FORBIDDEN — wastes the token budget and causes truncation):");
        sb.AppendLine("{");
        sb.AppendLine("  \"Cohort analysis\": [");
        sb.AppendLine("    { \"term\": \"Cohort analysis\", \"confidence\": 1.0 }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("Return ONLY the JSON object. No markdown fences, no commentary, no pretty-printing.");
        return sb.ToString();
    }
}
