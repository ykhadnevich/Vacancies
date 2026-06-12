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
/// Recruiter-side Mono scoring service. Mechanically identical to
/// <see cref="MonolithicScoringService"/> — same Gemini endpoint, response schema,
/// guardrails, and parsing — but renders the reason via
/// <see cref="RecruiterMonolithicScoringPromptV1"/> (third person, recruiter voice).
/// The distinct <see cref="Version"/> tag keeps these results out of the
/// candidate-side Mono cache by construction.
/// </summary>
public sealed class RecruiterMonolithicScoringService : IRecruiterScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<RecruiterMonolithicScoringService> _logger;
    private readonly ILlmTracer _tracer;
    private readonly IScoreCalibrator _calibrator;

    public string Version => RecruiterMonolithicScoringPromptV1.Version;

    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public RecruiterMonolithicScoringService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<RecruiterMonolithicScoringService> logger,
        ILlmTracer tracer,
        IScoreCalibrator calibrator)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
        _tracer = tracer;
        _calibrator = calibrator;
    }

    public async Task<ScoringResult> ScoreAsync(
        string cvId,
        Guid vacancyId,
        string cvSummaryJson,
        string vacancyAnalysisJson,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(vacancyAnalysisJson)
            || vacancyAnalysisJson.Trim().Length < 20)
        {
            _logger.LogWarning("Empty vacancy analysis for {CvId} × {VacancyId} — refusing to score",
                cvId, vacancyId);
            return MonolithicScoringService.Fallback(cvId, vacancyId, "empty_vacancy", 0, 0);
        }

        if (string.IsNullOrWhiteSpace(cvSummaryJson))
        {
            _logger.LogWarning("Empty CV summary for {CvId} × {VacancyId} — refusing to score",
                cvId, vacancyId);
            return MonolithicScoringService.Fallback(cvId, vacancyId, "empty_cv", 0, 0);
        }

        var prompt = RecruiterMonolithicScoringPromptV1.Build(cvSummaryJson, vacancyAnalysisJson);
        using var span = _tracer.StartSpan(
            name: "recruiter_monolithic_scoring",
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
            // Distinct stage label so cost telemetry separates recruiter Gemini spend.
            CostBreakdown.Track("recruiter_monolithic_scoring", sw.Elapsed.TotalMilliseconds, inputTokens, outputTokens);

            if (!TryExtractInnerJson(root, out var inner))
            {
                span.EndError(new InvalidOperationException("no_inner_json"));
                return MonolithicScoringService.Fallback(cvId, vacancyId, "no_inner_json", inputTokens, outputTokens);
            }

            using var cvDoc = JsonDocument.Parse(cvSummaryJson);
            var result = MonolithicScoringService.ParseAndCompose(
                cvId, vacancyId, inner, inputTokens, outputTokens,
                cvForGuardrails:      cvDoc.RootElement,
                vacancyForGuardrails: default,
                vacancyRawText:       vacancyAnalysisJson,
                logger:               _logger,
                versionOverride:      Version);

            // Post-hoc calibration — maps the raw composite onto a calibrated
            // value whose percentage matches the held-out gold distribution.
            // No-op when no calibrator file is configured (default production
            // behaviour). Applied BEFORE the downstream cap layer so the cap
            // thresholds continue to operate on the same scale they were
            // empirically tuned against.
            //
            // The "+cal:" suffix on ModelVersion is appended whenever the
            // calibrator is ENABLED (not only when it nudged the value) — the
            // recruiter UI uses that suffix as a transparency signal meaning
            // "this percentage went through the calibrator". A near-identity
            // pass on a particular score must still surface the badge or the
            // signal is misleading.
            var rawScore = result.Score;
            var calibratedScore = _calibrator.Calibrate(rawScore);
            if (_calibrator.IsEnabled)
            {
                _logger.LogDebug(
                    "Recruiter scoring calibrated: {Raw:F4} → {Calibrated:F4} (calibrator={Version})",
                    rawScore, calibratedScore, _calibrator.Version);
                result = result with
                {
                    Score = calibratedScore,
                    ModelVersion = $"{result.ModelVersion}+cal:{_calibrator.Version}"
                };
            }

            span.EndOk(new
            {
                score             = result.Score,
                score_raw         = rawScore,
                calibrator        = _calibrator.Version,
                verdict           = result.Verdict.ToString(),
                sub_scores        = result.SubScores,
                anti_flag_penalty = result.AntiFlagPenalty,
                confidence        = result.Confidence,
                reason_en         = result.ReasonEn,
                reason_uk         = result.ReasonUk,
                matched_skills       = result.Evidence.MatchedSkills,
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
            span.EndError(new TimeoutException("recruiter_monolithic_scoring timeout"));
            _logger.LogWarning("Recruiter Mono scoring timed out for {CvId} × {VacancyId}", cvId, vacancyId);
            return MonolithicScoringService.Fallback(cvId, vacancyId, "timeout", 0, 0);
        }
        catch (Exception ex)
        {
            span.EndError(ex);
            _logger.LogWarning(ex, "Recruiter Mono scoring failed for {CvId} × {VacancyId}", cvId, vacancyId);
            return MonolithicScoringService.Fallback(cvId, vacancyId, ex.GetType().Name, 0, 0);
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
                                              "Self-reported certainty about the sub_scores, 0..1."),
                ["matched_skills"]       = StringArray("Skills present in both CV and vacancy"),
                ["missing_must_haves"]   = StringArray("Must-have skills absent from CV"),
                ["triggered_anti_flags"] = StringArray("Anti-requirements that fired against this CV"),
                ["reason_en"]            = new Dictionary<string, object>
                {
                    ["type"]        = "STRING",
                    ["description"] = "Recruiter-facing reason in English, third person, 25-45 words"
                },
                ["reason_uk"]            = new Dictionary<string, object>
                {
                    ["type"]        = "STRING",
                    ["description"] = "Recruiter-facing reason in Ukrainian, third person, 25-45 words"
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
