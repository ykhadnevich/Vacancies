using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Diagnostics;
using Application.Common.Interfaces;
using Domain.Scoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Monolithic;


/// <summary>
/// Single-shot LLM scoring service: one Gemini call produces all 7 sub-scores
/// plus anti-flag penalty and evidence. The composite is then computed deterministically
/// in C# using <see cref="ScoringConstants.LinearWeights"/> — LLMs are unreliable
/// for multi-term weighted sums (96% inconsistency on internal benchmarks).
///
/// Replaces the seven C# sub-axis calculators when activated. Falls back gracefully
/// to a neutral score (0.5) on transport / parsing failures so the caller can route
/// to the deterministic <see cref="ScoringServiceV2"/> baseline.
/// </summary>
public sealed class MonolithicScoringService : IScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<MonolithicScoringService> _logger;

    // Tracks the prompt version so a prompt bump (e.g. v3_7_voice → v3_8_anchors)
    // automatically invalidates the Mono result cache. Without this link an
    // updated prompt would happily serve stale v3 entries from ScoringCache.
    public static string Version => MonolithicScoringPromptV3.Version;
    string IScoringService.Version => Version;

    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly ILlmTracer _tracer;

    public MonolithicScoringService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MonolithicScoringService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
    }

    public Task<ScoringResult> ScoreAsync(
        string cvId, Guid vacancyId,
        string cvSummaryJson, string vacancyAnalysisJson,
        CancellationToken ct = default,
        bool skipReason = false,
        bool skipJudge = false)
        => ScoreRawAsync(cvId, vacancyId, cvSummaryJson, vacancyAnalysisJson, Version, ct);


    public async Task<ScoringResult> ScoreRawAsync(
        string cvId, Guid vacancyId,
        string cvSummaryJson, string vacancyRawText,
        string promptVersion = "v3",
        CancellationToken ct = default)
    {

        // Without a vacancy we have nothing to score against.
        // Returning a neutral 1.0 (or letting the LLM hallucinate) would
        // surface this pair in the top-N for free — refuse instead.
        if (string.IsNullOrWhiteSpace(vacancyRawText)
            || vacancyRawText.Trim().Length < 20)
        {
            _logger.LogWarning("Empty/short vacancy text for {CvId} × {VacancyId} — refusing to score",
                cvId, vacancyId);
            return Fallback(cvId, vacancyId, "empty_vacancy", 0, 0);
        }

        if (string.IsNullOrWhiteSpace(cvSummaryJson))
        {
            _logger.LogWarning("Empty CV summary for {CvId} × {VacancyId} — refusing to score",
                cvId, vacancyId);
            return Fallback(cvId, vacancyId, "empty_cv", 0, 0);
        }

        var prompt = MonolithicScoringPromptV3.Build(cvSummaryJson, vacancyRawText);
        using var span = _tracer.StartSpan(
            name: "monolithic_scoring",
            runType: LlmRunType.LLM,
            inputs: new { cv_id = cvId, vacancy_id = vacancyId, model = Model, version = Version, prompt });

        try
        {
            var body = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {
                    temperature      = 0.1,
                    topP             = 0.95,
                    maxOutputTokens  = 2048,
                    thinkingConfig   = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema   = BuildResponseSchema(),
                }
            };

            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(20));

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var sw = Stopwatch.StartNew();
            var resp = await _httpClient.PostAsJsonAsync(url, body, perCallCts.Token);
            resp.EnsureSuccessStatusCode();
            var raw = await resp.Content.ReadAsStringAsync(perCallCts.Token);
            sw.Stop();

            using var doc = JsonDocument.Parse(raw);
            var root = doc.RootElement;

            var (inputTokens, outputTokens) = ExtractTokenUsage(root);
            CostBreakdown.Track("monolithic_scoring", sw.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!TryExtractInnerJson(root, out var inner))
            {
                span.EndError(new InvalidOperationException("no_inner_json"));
                return Fallback(cvId, vacancyId, "no_inner_json", inputTokens, outputTokens);
            }

            using var cvDoc = JsonDocument.Parse(cvSummaryJson);
            var result = ParseAndCompose(
                cvId, vacancyId, inner, inputTokens, outputTokens,
                cvForGuardrails:      cvDoc.RootElement,
                vacancyForGuardrails: default,
                vacancyRawText:       vacancyRawText,
                logger:               _logger);
            span.EndOk(new
            {
                score             = result.Score,
                verdict           = result.Verdict.ToString(),
                sub_scores        = result.SubScores,
                anti_flag_penalty = result.AntiFlagPenalty,
                confidence        = result.Confidence,
                reason_en         = result.ReasonEn,
                reason_uk         = result.ReasonUk,
                matched_skills    = result.Evidence.MatchedSkills,
                missing_must_haves   = result.Evidence.MissingMustHaves,
                triggered_anti_flags = result.Evidence.TriggeredAntiFlags,
                model_version = result.ModelVersion,
                input_tokens  = inputTokens,
                output_tokens = outputTokens,
                latency_ms    = sw.Elapsed.TotalMilliseconds
            });
            return result;
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            span.EndError(new TimeoutException("monolithic_scoring timeout"));
            _logger.LogWarning("Monolithic scoring timed out for {CvId} × {VacancyId}", cvId, vacancyId);
            return Fallback(cvId, vacancyId, "timeout", 0, 0);
        }
        catch (Exception ex)
        {
            span.EndError(ex);
            _logger.LogWarning(ex, "Monolithic scoring failed for {CvId} × {VacancyId}", cvId, vacancyId);
            return Fallback(cvId, vacancyId, ex.GetType().Name, 0, 0);
        }
    }


    private static (int input, int output) ExtractTokenUsage(JsonElement root)
    {
        int input = 0, output = 0;
        if (root.TryGetProperty("usageMetadata", out var usage))
        {
            if (usage.TryGetProperty("promptTokenCount", out var pIn)
                && pIn.ValueKind == JsonValueKind.Number)
                input = pIn.GetInt32();
            if (usage.TryGetProperty("candidatesTokenCount", out var pOut)
                && pOut.ValueKind == JsonValueKind.Number)
                output = pOut.GetInt32();
        }
        return (input, output);
    }


    private static bool TryExtractInnerJson(JsonElement root, out JsonElement inner)
    {
        inner = default;
        if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            return false;
        var first = cands[0];
        if (!first.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.GetArrayLength() == 0)
            return false;

        string text = string.Empty;
        foreach (var p in parts.EnumerateArray())
            if (p.TryGetProperty("text", out var t)) { text = t.GetString() ?? string.Empty; break; }
        if (string.IsNullOrWhiteSpace(text))
            return false;

        text = text.Replace("```json", string.Empty).Replace("```", string.Empty).Trim();
        try
        {
            var parsedDoc = JsonDocument.Parse(text);
            inner = parsedDoc.RootElement.Clone();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }


    public static ScoringResult ParseAndCompose(
        string cvId,
        Guid vacancyId,
        JsonElement root,
        int inputTokens,
        int outputTokens,
        JsonElement cvForGuardrails = default,
        JsonElement vacancyForGuardrails = default,
        string? vacancyRawText = null,
        ILogger? logger = null,
        // When the recruiter-side service calls ParseAndCompose it must report its
        // own prompt version so the cached ScoringResult / UI ModelVersion line up
        // with the prompt that actually ran. Defaults to Mono V3 for legacy callers.
        string? versionOverride = null)
    {
        if (!root.TryGetProperty("sub_scores", out var subEl)
            || subEl.ValueKind != JsonValueKind.Object)
            return Fallback(cvId, vacancyId, "missing_sub_scores", inputTokens, outputTokens);

        var subs = ReadSubScores(subEl);

        double antiPenalty = ScoringConstants.AntiFlag.PenaltyNone;
        if (root.TryGetProperty("anti_flag_penalty", out var penEl)
            && penEl.ValueKind == JsonValueKind.Number)
        {
            antiPenalty = Math.Clamp(penEl.GetDouble(), 0.0, 1.0);
        }


        double confidence = 1.0;
        if (root.TryGetProperty("confidence", out var confEl)
            && confEl.ValueKind == JsonValueKind.Number)
        {
            confidence = Math.Clamp(confEl.GetDouble(), 0.0, 1.0);
        }

        var matched      = ReadStringArray(root, "matched_skills");
        var missing      = ReadStringArray(root, "missing_must_haves");
        var triggered    = ReadStringArray(root, "triggered_anti_flags");

        var reasonEn = ReadString(root, "reason_en") ?? string.Empty;
        var reasonUk = ReadString(root, "reason_uk");


        // Deterministic safety nets: cap sub-scores when the LLM ignored hard
        // signals (seniority gap, cross-stack mismatch). Stays a no-op when
        // we don't have the inputs to evaluate the guards (e.g. in unit tests
        // that exercise ParseAndCompose directly without a full CV/vacancy).
        var versionTag = versionOverride ?? Version;
        if (cvForGuardrails.ValueKind == JsonValueKind.Object)
        {
            var (capped, report) = MonolithicGuardrails.Apply(
                subs, cvForGuardrails, vacancyForGuardrails, vacancyRawText);
            if (report.UnderQualifiedTriggered || report.CrossStackTriggered)
            {
                subs = capped;
                versionTag = (versionOverride ?? MonolithicScoringPromptV3.Version) + "+guardrails";
                logger?.LogInformation(
                    "Monolithic guardrails triggered for {CvId} × {VacancyId}: {Reason}",
                    cvId, vacancyId, report.Reason);
            }
        }

        double weightedSum = ComputeWeightedSum(subs);
        double composite = Math.Clamp(weightedSum * antiPenalty, 0.0, 1.0);
        var verdict = VerdictExtensions.FromScore(composite);

        return new ScoringResult(
            VacancyId:       vacancyId,
            CvId:            cvId,
            ModelVersion:    versionTag,
            GeneratedAt:     DateTime.UtcNow,
            Score:           composite,
            SubScores:       subs,
            AntiFlagPenalty: antiPenalty,
            ReasonEn:        reasonEn,
            ReasonUk:        reasonUk,
            Evidence:        new ScoringEvidence(matched, missing, triggered),
            InputTokens:     inputTokens,
            OutputTokens:    outputTokens,
            Verdict:         verdict,
            Context:         null,
            Confidence:      confidence);
    }


    public static double ComputeWeightedSum(SubScores s) =>
        s.SkillMatch       * ScoringConstants.LinearWeights.Skill      +
        s.SeniorityMatch   * ScoringConstants.LinearWeights.Seniority  +
        s.ExperienceMatch  * ScoringConstants.LinearWeights.Experience +
        s.RoleIntentMatch  * ScoringConstants.LinearWeights.RoleIntent +
        s.DomainAlignment  * ScoringConstants.LinearWeights.Domain     +
        s.LanguageMatch    * ScoringConstants.LinearWeights.Language   +
        s.EducationMatch   * ScoringConstants.LinearWeights.Education;


    private static SubScores ReadSubScores(JsonElement el) =>
        new(
            SkillMatch:      ReadClampedDouble(el, "skill_match"),
            SeniorityMatch:  ReadClampedDouble(el, "seniority_match"),
            ExperienceMatch: ReadClampedDouble(el, "experience_match"),
            LanguageMatch:   ReadClampedDouble(el, "language_match"),
            EducationMatch:  ReadClampedDouble(el, "education_match"),
            RoleIntentMatch: ReadClampedDouble(el, "role_intent_match"),
            DomainAlignment: ReadClampedDouble(el, "domain_alignment"));


    private static double ReadClampedDouble(JsonElement el, string field)
    {
        if (el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.Number)
            return Math.Clamp(v.GetDouble(), 0.0, 1.0);
        return 0.0;
    }


    private static List<string> ReadStringArray(JsonElement el, string field)
    {
        var list = new List<string>();
        if (!el.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return list;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var s = item.GetString()?.Trim();
            if (!string.IsNullOrEmpty(s)) list.Add(s);
        }
        return list;
    }


    private static string? ReadString(JsonElement el, string field)
    {
        if (el.TryGetProperty(field, out var v) && v.ValueKind == JsonValueKind.String)
            return v.GetString();
        return null;
    }


    /// <summary>
    /// Neutral 0.5 result used when the LLM call fails. Callers should treat
    /// <see cref="ScoringResult.ModelVersion"/> ending in <c>_fallback</c>
    /// as a signal to route the pair through the deterministic baseline.
    /// </summary>
    public static ScoringResult Fallback(
        string cvId, Guid vacancyId, string reason, int inputTokens, int outputTokens)
    {
        const double stub = 0.5;
        var subs = new SubScores(stub, stub, stub, stub, stub, stub, stub);
        return new ScoringResult(
            VacancyId:       vacancyId,
            CvId:            cvId,
            ModelVersion:    Version + "_fallback:" + reason,
            GeneratedAt:     DateTime.UtcNow,
            Score:           stub,
            SubScores:       subs,
            AntiFlagPenalty: ScoringConstants.AntiFlag.PenaltyNone,
            ReasonEn:        "Monolithic scoring fallback (" + reason + ").",
            ReasonUk:        "Monolithic scoring fallback (" + reason + ").",
            Evidence:        new ScoringEvidence(
                                  Array.Empty<string>(),
                                  Array.Empty<string>(),
                                  Array.Empty<string>()),
            InputTokens:     inputTokens,
            OutputTokens:    outputTokens,
            Verdict:         VerdictExtensions.FromScore(stub),
            Context:         null,
            Confidence:      0.0);
    }


    private static Dictionary<string, object> BuildResponseSchema()
    {
        Dictionary<string, object> NumberInUnitInterval(string description) => new()
        {
            ["type"]        = "NUMBER",
            ["description"] = description
        };

        Dictionary<string, object> StringArray(string description) => new()
        {
            ["type"]        = "ARRAY",
            ["items"]       = new Dictionary<string, object> { ["type"] = "STRING" },
            ["description"] = description
        };

        var subScoresProps = new Dictionary<string, object>
        {
            ["skill_match"]       = NumberInUnitInterval("Skill overlap, 0..1"),
            ["seniority_match"]   = NumberInUnitInterval("Seniority fit, 0..1"),
            ["experience_match"]  = NumberInUnitInterval("Years-of-experience fit, 0..1"),
            ["language_match"]    = NumberInUnitInterval("Language fit, 0..1"),
            ["education_match"]   = NumberInUnitInterval("Education fit, 0..1"),
            ["role_intent_match"] = NumberInUnitInterval("Role-intent closeness, 0..1"),
            ["domain_alignment"]  = NumberInUnitInterval("Domain alignment, 0..1"),
        };

        return new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["sub_scores"] = new Dictionary<string, object>
                {
                    ["type"]       = "OBJECT",
                    ["properties"] = subScoresProps,
                    ["required"]   = subScoresProps.Keys.ToArray(),
                },
                ["anti_flag_penalty"]    = NumberInUnitInterval("Multiplicative penalty, 0.2 / 0.5 / 1.0"),
                ["confidence"]           = NumberInUnitInterval(
                                              "Self-reported certainty about the sub_scores, 0..1. " +
                                              "Lower the value when inputs are sparse or ambiguous."),
                ["matched_skills"]       = StringArray("Skills present in both CV and vacancy"),
                ["missing_must_haves"]   = StringArray("Must-have skills absent from CV"),
                ["triggered_anti_flags"] = StringArray("Anti-requirements that fired against this CV"),
                ["reason_en"]            = new Dictionary<string, object>
                {
                    ["type"]        = "STRING",
                    ["description"] = "Bilingual reason — English, ≤25 words"
                },
                ["reason_uk"]            = new Dictionary<string, object>
                {
                    ["type"]        = "STRING",
                    ["description"] = "Bilingual reason — Ukrainian, ≤25 words"
                },
            },
            ["required"] = new[]
            {
                "sub_scores",
                "anti_flag_penalty",
                "confidence",
                "matched_skills",
                "missing_must_haves",
                "triggered_anti_flags",
                "reason_en",
                "reason_uk"
            }
        };
    }
}
