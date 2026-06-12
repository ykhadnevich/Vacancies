using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Application.Common.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring;


public sealed class GeminiCompositeJudgeService : ICompositeJudgeService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiCompositeJudgeService> _logger;

    private const string Model   = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const string PromptVersion = "judge_v7_1_confidence";

    private readonly ILlmTracer _tracer;

    public GeminiCompositeJudgeService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<GeminiCompositeJudgeService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<JudgeResult> JudgeAsync(
        JsonElement cvSummary,
        JsonElement vacancyAnalysis,
        SubScores subScores,
        ScoringEvidence evidence,
        double initialScore,
        Verdict initialVerdict,
        CancellationToken ct = default)
    {
        var family = RoleFamilyDetector.Detect(cvSummary);
        var prompt = BuildPrompt(cvSummary, vacancyAnalysis, subScores, evidence,
                                 initialScore, initialVerdict, family);

        using var span = _tracer.StartSpan(
            name: "composite_judge",
            runType: LlmRunType.LLM,
            inputs: new { family = family.ToString(), model = Model, version = PromptVersion, initial_score = initialScore, prompt });

        try
        {
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature      = 0.1,
                    topP             = 0.95,
                    maxOutputTokens  = 1024,
                    thinkingConfig   = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema = new Dictionary<string, object>
                    {
                        ["type"] = "OBJECT",
                        ["properties"] = new Dictionary<string, object>
                        {
                            ["final_score"] = new Dictionary<string, object>
                            {
                                ["type"]        = "NUMBER",
                                ["description"] = "Composite score in [0,1]"
                            },
                            ["confidence"] = new Dictionary<string, object>
                            {
                                ["type"]        = "NUMBER",
                                ["description"] = "Self-reported certainty about this score in [0,1]. " +
                                                  "1.0 = both CV and vacancy detailed, overlap unambiguous. " +
                                                  "0.4 = significant missing info — flag for human review."
                            }
                        },
                        ["required"] = new[] { "final_score", "confidence" }
                    }
                }
            };

            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(15));

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
            CostBreakdown.Track("composite_judge", swCall.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
                return Fallback(initialScore, initialVerdict, "no_candidates");
            var first = cands[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
                return Fallback(initialScore, initialVerdict, "no_content_parts");

            string text = string.Empty;
            foreach (var p in parts.EnumerateArray())
                if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? string.Empty; break; }
            if (string.IsNullOrWhiteSpace(text))
                return Fallback(initialScore, initialVerdict, "empty_text");

            text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
            using var scoreDoc = JsonDocument.Parse(text);
            if (!scoreDoc.RootElement.TryGetProperty("final_score", out var fsEl)
                || fsEl.ValueKind != JsonValueKind.Number)
                return Fallback(initialScore, initialVerdict, "missing_or_malformed_final_score");

            double finalScore = Math.Clamp(fsEl.GetDouble(), 0.0, 1.0);
            var finalVerdict = VerdictExtensions.FromScore(finalScore);
            double? confidence = null;
            if (scoreDoc.RootElement.TryGetProperty("confidence", out var confEl)
                && confEl.ValueKind == JsonValueKind.Number)
                confidence = Math.Clamp(confEl.GetDouble(), 0.0, 1.0);
            span.EndOk(new
            {
                final_score   = finalScore,
                verdict       = finalVerdict.ToString(),
                confidence    = confidence,
                initial_score = initialScore,
                family        = family.ToString(),
                input_tokens  = inputTokens,
                output_tokens = outputTokens
            });
            return new JudgeResult(
                FinalScore:    finalScore,
                FinalVerdict:  finalVerdict,
                InputTokens:   inputTokens,
                OutputTokens:  outputTokens,
                FallbackUsed:  false,
                FailureReason: null);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            span.EndError(new TimeoutException("composite_judge timeout"));
            _logger.LogWarning("Composite judge timed out");
            return Fallback(initialScore, initialVerdict, "timeout");
        }
        catch (Exception ex)
        {
            span.EndError(ex);
            _logger.LogWarning(ex, "Composite judge failed");
            return Fallback(initialScore, initialVerdict, ex.GetType().Name);
        }
    }

    private static JudgeResult Fallback(double initialScore, Verdict initialVerdict, string reason)
        => new(initialScore, initialVerdict, 0, 0, FallbackUsed: true, FailureReason: reason);


    private static string BuildPrompt(
        JsonElement cv, JsonElement vacancy,
        SubScores subScores, ScoringEvidence evidence,
        double initialScore, Verdict initialVerdict,
        RoleFamily family)
    {
        static string Compact(JsonElement el)
            => JsonSerializer.Serialize(el, new JsonSerializerOptions { WriteIndented = false });

        var matched = evidence.MatchedSkills.Count == 0
            ? "(none)" : string.Join(", ", evidence.MatchedSkills);
        var missing = evidence.MissingMustHaves.Count == 0
            ? "(none)" : string.Join(", ", evidence.MissingMustHaves);
        var antiFlags = evidence.TriggeredAntiFlags.Count == 0
            ? "(none)" : string.Join(", ", evidence.TriggeredAntiFlags);

        var sb = new System.Text.StringBuilder(8192);
        sb.AppendLine("You evaluate how well a candidate's CV matches a job vacancy.");
        sb.AppendLine("Produce ONE composite score representing real fit.");
        sb.AppendLine();
        sb.AppendLine("=== INPUT ===");
        sb.AppendLine();
        sb.AppendLine("CV (normalized JSON):");
        sb.AppendLine(Compact(cv));
        sb.AppendLine();
        sb.AppendLine("VACANCY (normalized JSON):");
        sb.AppendLine(Compact(vacancy));
        sb.AppendLine();
        sb.AppendLine("DETERMINISTIC SUB-SCORES (each in [0,1]):");
        sb.AppendLine($"  skill_match       = {subScores.SkillMatch:F3}");
        sb.AppendLine($"  seniority_match   = {subScores.SeniorityMatch:F3}");
        sb.AppendLine($"  experience_match  = {subScores.ExperienceMatch:F3}");
        sb.AppendLine($"  language_match    = {subScores.LanguageMatch:F3}");
        sb.AppendLine($"  education_match   = {subScores.EducationMatch:F3}");
        sb.AppendLine($"  role_intent_match = {subScores.RoleIntentMatch:F3}");
        sb.AppendLine($"  domain_alignment  = {subScores.DomainAlignment:F3}");
        sb.AppendLine();
        sb.AppendLine("EVIDENCE:");
        sb.AppendLine($"  matched_skills:        {matched}");
        sb.AppendLine($"  missing_must_haves:    {missing}");
        sb.AppendLine($"  triggered_anti_flags:  {antiFlags}");
        sb.AppendLine();
        sb.AppendLine("LINEAR-FORMULA INITIAL ANCHOR:");
        sb.AppendLine($"  initial_score   = {initialScore:F3}");
        sb.AppendLine($"  initial_verdict = {initialVerdict}");
        sb.AppendLine();


        sb.Append(JudgePromptCore.Build(family));

        sb.AppendLine();
        sb.AppendLine("=== OUTPUT ===");
        sb.AppendLine();
        sb.AppendLine("Return ONLY a JSON object with two fields:");
        sb.AppendLine("  final_score (number in [0,1])");
        sb.AppendLine("  confidence  (number in [0,1] — see confidence guide above)");
        sb.AppendLine();
        sb.AppendLine("Example: {\"final_score\":0.83,\"confidence\":0.9}");
        sb.AppendLine();
        sb.AppendLine("No reasoning, no markdown, no other fields.");
        return sb.ToString();
    }
}
