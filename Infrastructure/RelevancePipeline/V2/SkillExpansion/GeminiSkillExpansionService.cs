using System.Diagnostics;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.SkillExpansion;


public sealed class GeminiSkillExpansionService : ISkillExpansionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiSkillExpansionService> _logger;
    private readonly ILlmTracer _tracer;

    private const string Model   = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";


    public string Version => "expand_v3+" + Model;

    public GeminiSkillExpansionService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiSkillExpansionService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<SkillExpansionResult> ExpandAsync(
        IReadOnlyList<string> skills,
        string skillType,
        string? roleFamilyHint,
        CancellationToken ct = default)
    {

        if (skills.Count == 0)
            return new SkillExpansionResult("{}", 0, 0, FallbackUsed: false, FailureReason: null);

        var prompt = BuildPrompt(skills, skillType, roleFamilyHint);
        using var span = _tracer.StartSpan(
            name: "skill_expansion",
            runType: LlmRunType.LLM,
            inputs: new { prompt, model = Model, version = Version, skill_count = skills.Count, skill_type = skillType, role_family_hint = roleFamilyHint });
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
            var json = await resp.Content.ReadAsStringAsync(perCallCts.Token);
            swCall.Stop();

            using var doc = JsonDocument.Parse(json);
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

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            {
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = true, reason = "no_candidates" });
                return Identity(skills, "no candidates");
            }

            var first = cands[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
            {
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = true, reason = "no_parts" });
                return Identity(skills, "no parts");
            }

            string text = "";
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? ""; break; }
            if (string.IsNullOrWhiteSpace(text))
            {
                span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = true, reason = "empty_text" });
                return Identity(skills, "empty text");
            }

            text = text.Replace("```json", "").Replace("```", "").Trim();


            string validated = ValidateAndPatch(text, skills);

            CostBreakdown.Track("skill_expansion", swCall.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            _logger.LogDebug(
                "Skill expansion ({Version}): {Count} skills expanded, tokens in={In}, out={Out}",
                Version, skills.Count, inputTokens, outputTokens);

            JsonElement? extractedJson = null;
            try
            {
                using var extractedDoc = JsonDocument.Parse(validated);
                extractedJson = extractedDoc.RootElement.Clone();
            }
            catch (JsonException) { /* tracing best-effort */ }

            span.EndOk(new { input_tokens = inputTokens, output_tokens = outputTokens, latency_ms = swCall.Elapsed.TotalMilliseconds, version = Version, fallback = false, skills_in = skills.Count, extracted = extractedJson });
            return new SkillExpansionResult(
                ExpansionJson: validated,
                InputTokens:   inputTokens,
                OutputTokens:  outputTokens,
                FallbackUsed:  false,
                FailureReason: null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning("Skill expansion timed out -- using identity fallback");
            span.EndError(new TimeoutException("skill_expansion timeout"));
            return Identity(skills, "per-call timeout");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Skill expansion failed -- using identity fallback");
            span.EndError(ex);
            return Identity(skills, ex.GetType().Name + ": " + ex.Message);
        }
    }


    private SkillExpansionResult Identity(IReadOnlyList<string> skills, string reason)
    {
        var sb = new StringBuilder(256);
        sb.Append('{');
        bool first = true;
        foreach (var s in skills)
        {
            if (!first) sb.Append(',');


            sb.Append(JsonSerializer.Serialize(s));


            sb.Append(":[{\"term\":");
            sb.Append(JsonSerializer.Serialize(s));
            sb.Append(",\"confidence\":1.0}]");
            first = false;
        }
        sb.Append('}');
        return new SkillExpansionResult(sb.ToString(), 0, 0, FallbackUsed: true, FailureReason: reason);
    }


    private static string ValidateAndPatch(string llmJson, IReadOnlyList<string> skills)
    {
        try
        {
            using var doc = JsonDocument.Parse(llmJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object) return llmJson;


            var produced = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
            foreach (var prop in doc.RootElement.EnumerateObject())
                produced[prop.Name] = prop.Value;

            var sb = new StringBuilder(llmJson.Length + 256);
            sb.Append('{');
            bool first = true;
            foreach (var skill in skills)
            {
                if (!first) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(skill));
                sb.Append(':');

                if (produced.TryGetValue(skill, out var v) && v.ValueKind == JsonValueKind.Array)
                {


                    bool hasSelf = false;
                    foreach (var item in v.EnumerateArray())
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
                        sb.Append(v.GetRawText());
                    }
                    else
                    {
                        sb.Append("[{\"term\":");
                        sb.Append(JsonSerializer.Serialize(skill));
                        sb.Append(",\"confidence\":1.0}");
                        foreach (var item in v.EnumerateArray())
                        {
                            sb.Append(',');
                            sb.Append(item.GetRawText());
                        }
                        sb.Append(']');
                    }
                }
                else
                {

                    sb.Append("[{\"term\":");
                    sb.Append(JsonSerializer.Serialize(skill));
                    sb.Append(",\"confidence\":1.0}]");
                }
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }
        catch
        {


            var sb = new StringBuilder(256);
            sb.Append('{');
            bool first = true;
            foreach (var s in skills)
            {
                if (!first) sb.Append(',');
                sb.Append(JsonSerializer.Serialize(s));
                sb.Append(":[{\"term\":");
                sb.Append(JsonSerializer.Serialize(s));
                sb.Append(",\"confidence\":1.0}]");
                first = false;
            }
            sb.Append('}');
            return sb.ToString();
        }
    }

    private static string BuildPrompt(
        IReadOnlyList<string> skills, string skillType, string? roleFamilyHint)
    {
        bool isTechnical = string.Equals(skillType, "technical", StringComparison.OrdinalIgnoreCase);

        var sb = new StringBuilder(2048);
        sb.AppendLine("You are a skill ontology expander for a job-matching system.");
        sb.AppendLine();
        sb.AppendLine("Input: a list of skill names extracted from a CV or vacancy.");
        sb.AppendLine("Output: a JSON object mapping each input skill to an array of typed");
        sb.AppendLine("expansion terms.");
        sb.AppendLine();
        sb.AppendLine("=== RULES ===");
        sb.AppendLine();
        sb.AppendLine("1. ADDITIVE -- the original skill must always appear in its own array");
        sb.AppendLine("   as the first entry with relation \"self\" and confidence 1.0.");
        sb.AppendLine("   Never replace the original; only add expansion terms beside it.");
        sb.AppendLine();
        sb.AppendLine("2. RELATIONS -- each expansion entry has a typed relation:");
        sb.AppendLine("     \"self\"            -- the original token (always present, conf 1.0)");
        sb.AppendLine("     \"synonym\"         -- different surface form, same concept");
        sb.AppendLine("                          (e.g. \"A/B testing\" <-> \"A/B-тестів\" <-> \"experimentation\")");
        sb.AppendLine("     \"language_variant\"-- translation/transliteration of the same term");
        sb.AppendLine("                          (e.g. \"A/B testing\" -> \"А/Б тестування\")");
        sb.AppendLine("     \"parent\"          -- broader concept the original belongs to");
        sb.AppendLine("                          (e.g. \"Cohort analysis\" -> \"product analytics\")");
        sb.AppendLine("     \"brand_generic\"   -- generic category the brand product fulfills");
        sb.AppendLine("                          (e.g. \"Stripe Billing\" -> \"billing\", \"payment processing\")");
        sb.AppendLine();
        sb.AppendLine("3. CONFIDENCE -- a number in (0,1]:");
        sb.AppendLine("     1.0    = self");
        sb.AppendLine("     0.9+   = synonym or language_variant");
        sb.AppendLine("     0.7-0.9= brand_generic, well-known parent");
        sb.AppendLine("     0.5-0.7= weaker parent or domain-specific synonym");

        if (isTechnical)
        {
            sb.AppendLine();
            sb.AppendLine("4. TECHNICAL skill type -- BE CONSERVATIVE.");
            sb.AppendLine("   Only expand to:");
            sb.AppendLine("     - direct synonyms (Postman -> REST client)");
            sb.AppendLine("     - tool families ( Postman -> API testing tools )");
            sb.AppendLine("     - language variants");
            sb.AppendLine("   Do NOT expand to disciplines or job roles.");
            sb.AppendLine("   BAD: Jira -> project management. Jira is a TOOL.");
            sb.AppendLine("   GOOD: Jira -> issue tracker, ticketing system, Atlassian tools.");
            sb.AppendLine("   Aim for 1-2 expansions per skill, max 3.");
        }
        else
        {
            sb.AppendLine();
            sb.AppendLine("4. DOMAIN skill type -- BE AGGRESSIVE within its concept space.");
            sb.AppendLine("   Expand to parents, synonyms, well-known examples, language variants.");
            sb.AppendLine("   Example: \"Cohort analysis\" ->");
            sb.AppendLine("     synonyms: \"retention analytics\", \"user segmentation\"");
            sb.AppendLine("     parents:  \"product analytics\", \"behavioral analytics\"");
            sb.AppendLine("     variants: \"когортний аналіз\"");
            sb.AppendLine("   But preserve specialization -- do NOT erase domain detail.");
            sb.AppendLine("   BAD: \"Bayesian A/B testing with CUPED\" -> just \"A/B testing\".");
            sb.AppendLine("   GOOD: include \"CUPED\", \"variance reduction\", \"Bayesian inference\"");
            sb.AppendLine("         alongside the original.");
            sb.AppendLine("   Aim for 2-3 expansions per skill, max 4.");
        }

        if (!string.IsNullOrWhiteSpace(roleFamilyHint))
        {
            sb.AppendLine();
            sb.AppendLine("5. ROLE-FAMILY CONTEXT (use to disambiguate, NOT to filter):");
            sb.AppendLine("   Vacancy targets: " + roleFamilyHint);
            sb.AppendLine("   When a skill has multiple interpretations across role families,");
            sb.AppendLine("   pick the one consistent with this role family. Do NOT drop");
            sb.AppendLine("   skills that don't fit this role -- the CV side has no such hint.");
        }

        sb.AppendLine();
        sb.AppendLine("=== OUTPUT SHAPE ===");
        sb.AppendLine();
        sb.AppendLine("Return ONLY a JSON object. Keys = original skill names (verbatim).");
        sb.AppendLine("Values = arrays of {term, confidence} entries. NO relation field.");
        sb.AppendLine("The relation taxonomy above is for YOUR reasoning only -- it controls");
        sb.AppendLine("WHICH terms to add and what confidence to assign, but never appears");
        sb.AppendLine("in the output. This is a deliberate token-budget choice.");
        sb.AppendLine();
        sb.AppendLine("Example (target shape; keep it this compact):");
        sb.AppendLine("{");
        sb.AppendLine("  \"Cohort analysis\": [");
        sb.AppendLine("    {\"term\":\"Cohort analysis\",\"confidence\":1.0},");
        sb.AppendLine("    {\"term\":\"retention analytics\",\"confidence\":0.9},");
        sb.AppendLine("    {\"term\":\"product analytics\",\"confidence\":0.8}");
        sb.AppendLine("  ],");
        sb.AppendLine("  \"Stripe Billing\": [");
        sb.AppendLine("    {\"term\":\"Stripe Billing\",\"confidence\":1.0},");
        sb.AppendLine("    {\"term\":\"billing\",\"confidence\":0.9},");
        sb.AppendLine("    {\"term\":\"payment processing\",\"confidence\":0.85}");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();
        sb.AppendLine("=== INPUT ===");
        sb.AppendLine();
        sb.Append("skill_type: ");
        sb.AppendLine(isTechnical ? "technical" : "domain");
        sb.AppendLine();
        sb.AppendLine("skills:");
        foreach (var s in skills) { sb.Append("  - "); sb.AppendLine(s); }
        sb.AppendLine();
        sb.AppendLine("Return the JSON object now. No prose, no markdown.");
        return sb.ToString();
    }
}
