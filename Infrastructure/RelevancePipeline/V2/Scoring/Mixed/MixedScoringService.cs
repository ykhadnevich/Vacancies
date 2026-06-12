using System.Net.Http.Json;
using System.Text.Json;
using Domain.Scoring;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Mixed;


public sealed class MixedScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<MixedScoringService> _logger;

    public const string Version = MixedScoringPrompt.Version;

    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public MixedScoringService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<MixedScoringService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
    }

    public async Task<ScoringResult> ScoreAsync(
        string cvId, Guid vacancyId,
        string rawCvText, string normalizedVacancyJson,
        CancellationToken ct = default)
    {
        var prompt = MixedScoringPrompt.Build(rawCvText, normalizedVacancyJson);

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                temperature = 0,
                topK = 1,
                topP = 0.01,
                maxOutputTokens = 2048,
                thinkingConfig = new { thinkingBudget = 0 },
                responseMimeType = "application/json",
                responseSchema = BuildResponseSchema()
            }
        };

        var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
        var resp = await _httpClient.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();
        var responseText = await resp.Content.ReadAsStringAsync(ct);

        return ParseResponse(cvId, vacancyId, responseText);
    }

    private ScoringResult ParseResponse(string cvId, Guid vacancyId, string geminiResponse)
    {
        using var envelope = JsonDocument.Parse(geminiResponse);
        var root = envelope.RootElement;
        if (!root.TryGetProperty("candidates", out var cands) || cands.GetArrayLength() == 0)
            throw new InvalidOperationException("Gemini returned no candidates");
        var first = cands[0];
        if (!first.TryGetProperty("content", out var content)
            || !content.TryGetProperty("parts", out var parts)
            || parts.GetArrayLength() == 0)
            throw new InvalidOperationException("Gemini response missing content/parts");

        string innerJson = "";
        foreach (var p in parts.EnumerateArray())
            if (p.TryGetProperty("text", out var t)) { innerJson = t.GetString() ?? ""; break; }
        innerJson = innerJson.Replace("```json", "").Replace("```", "").Trim();

        using var inner = JsonDocument.Parse(innerJson);
        var r = inner.RootElement;

        double score = ReadDouble(r, "score");
        double antiPen = ReadDouble(r, "anti_flag_penalty", 1.0);

        var ss = r.TryGetProperty("sub_scores", out var ssEl) && ssEl.ValueKind == JsonValueKind.Object
            ? ssEl : default;

        var subScores = new SubScores(
            SkillMatch:      ReadDouble(ss, "skill_match"),
            SeniorityMatch:  ReadDouble(ss, "seniority_match"),
            ExperienceMatch: ReadDouble(ss, "experience_match"),
            LanguageMatch:   ReadDouble(ss, "language_match"),
            EducationMatch:  ReadDouble(ss, "education_match"),
            RoleIntentMatch: ReadDouble(ss, "role_intent_match"),
            DomainAlignment: ReadDouble(ss, "domain_alignment"));

        var evidence = new ScoringEvidence(
            MatchedSkills:      ReadStringArray(r, "matched_skills"),
            MissingMustHaves:   ReadStringArray(r, "missing_must_haves"),
            TriggeredAntiFlags: ReadStringArray(r, "triggered_anti_flags"));

        return new ScoringResult(
            VacancyId: vacancyId,
            CvId: cvId,
            ModelVersion: Version,
            GeneratedAt: DateTime.UtcNow,
            Score: Math.Clamp(score, 0.0, 1.0),
            SubScores: subScores,
            AntiFlagPenalty: antiPen,
            ReasonEn: r.TryGetProperty("reason_en", out var en) ? en.GetString() ?? "" : "",
            ReasonUk: r.TryGetProperty("reason_uk", out var uk) ? uk.GetString() : null,
            Evidence: evidence);
    }

    private static double ReadDouble(JsonElement obj, string field, double fallback = 0)
    {
        if (obj.ValueKind != JsonValueKind.Object) return fallback;
        if (!obj.TryGetProperty(field, out var v)) return fallback;
        return v.ValueKind == JsonValueKind.Number ? v.GetDouble() : fallback;
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement obj, string field)
    {
        if (obj.ValueKind != JsonValueKind.Object) return Array.Empty<string>();
        if (!obj.TryGetProperty(field, out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<string>();
        var list = new List<string>(arr.GetArrayLength());
        foreach (var e in arr.EnumerateArray())
            if (e.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(e.GetString()))
                list.Add(e.GetString()!);
        return list;
    }

    private static object BuildResponseSchema() => new Dictionary<string, object>
    {
        ["type"] = "OBJECT",
        ["properties"] = new Dictionary<string, object>
        {
            ["score"] = new Dictionary<string, object> { ["type"] = "NUMBER" },
            ["anti_flag_penalty"] = new Dictionary<string, object> { ["type"] = "NUMBER" },
            ["sub_scores"] = new Dictionary<string, object>
            {
                ["type"] = "OBJECT",
                ["properties"] = new Dictionary<string, object>
                {
                    ["skill_match"]       = new Dictionary<string, object> { ["type"] = "NUMBER" },
                    ["seniority_match"]   = new Dictionary<string, object> { ["type"] = "NUMBER" },
                    ["experience_match"]  = new Dictionary<string, object> { ["type"] = "NUMBER" },
                    ["language_match"]    = new Dictionary<string, object> { ["type"] = "NUMBER" },
                    ["education_match"]   = new Dictionary<string, object> { ["type"] = "NUMBER" },
                    ["role_intent_match"] = new Dictionary<string, object> { ["type"] = "NUMBER" },
                    ["domain_alignment"]  = new Dictionary<string, object> { ["type"] = "NUMBER" },
                },
                ["required"] = new[] {
                    "skill_match", "seniority_match", "experience_match", "language_match",
                    "education_match", "role_intent_match", "domain_alignment"
                }
            },
            ["matched_skills"]       = new Dictionary<string, object> { ["type"] = "ARRAY", ["items"] = new Dictionary<string, object> { ["type"] = "STRING" } },
            ["missing_must_haves"]   = new Dictionary<string, object> { ["type"] = "ARRAY", ["items"] = new Dictionary<string, object> { ["type"] = "STRING" } },
            ["triggered_anti_flags"] = new Dictionary<string, object> { ["type"] = "ARRAY", ["items"] = new Dictionary<string, object> { ["type"] = "STRING" } },
            ["reason_en"] = new Dictionary<string, object> { ["type"] = "STRING" },
            ["reason_uk"] = new Dictionary<string, object> { ["type"] = "STRING" },
        },
        ["required"] = new[] {
            "score", "sub_scores", "anti_flag_penalty",
            "matched_skills", "missing_must_haves", "triggered_anti_flags",
            "reason_en", "reason_uk"
        }
    };
}
