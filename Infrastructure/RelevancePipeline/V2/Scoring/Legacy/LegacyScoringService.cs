using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline.V2.Scoring.Legacy;


public sealed class LegacyScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<LegacyScoringService> _logger;

    public const string Version = "scoring_legacy_v23";

    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public LegacyScoringService(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<LegacyScoringService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger = logger;
    }


#pragma warning disable CS0618
    public async Task<ScoringResult> ScoreAsync(
        string cvId, Guid vacancyId,
        string cvText,
        string vacancyTitle, string vacancyCompany, string vacancyDescription,
        CancellationToken ct = default)
    {


        var prompt = ScoringPrompt.Build(
            userProfileText: cvText,
            title:           vacancyTitle,
            company:         vacancyCompany,
            description:     vacancyDescription,
            roleYears:       null);

        var body = new
        {
            contents = new[] { new { parts = new[] { new { text = prompt } } } },
            generationConfig = new
            {
                temperature = 0,
                topK = 1,
                topP = 0.01,
                maxOutputTokens = 1024,
                thinkingConfig = new { thinkingBudget = 0 },


                responseMimeType = "application/json"
            }
        };

        var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
        var resp = await _httpClient.PostAsJsonAsync(url, body, ct);
        resp.EnsureSuccessStatusCode();
        var responseText = await resp.Content.ReadAsStringAsync(ct);

        return ParseResponse(cvId, vacancyId, responseText);
    }
#pragma warning restore CS0618

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


        double rawScore = r.TryGetProperty("score", out var sc) && sc.ValueKind == JsonValueKind.Number
            ? sc.GetDouble() : 50.0;
        double composite = Math.Clamp(rawScore / 100.0, 0.0, 1.0);


        var matchedSkills = new List<string>();
        if (r.TryGetProperty("matched", out var m) && m.ValueKind == JsonValueKind.String)
        {
            var matchedStr = m.GetString() ?? "";
            if (!string.Equals(matchedStr.Trim(), "none", StringComparison.OrdinalIgnoreCase))
            {
                foreach (var s in matchedStr.Split(','))
                {
                    var trimmed = s.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) matchedSkills.Add(trimmed);
                }
            }
        }


        var missingMustHaves = new List<string>();
        var antiFlagsList = new List<string>();
        if (r.TryGetProperty("gaps", out var gapsEl) && gapsEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var g in gapsEl.EnumerateArray())
            {
                if (g.ValueKind != JsonValueKind.Object) continue;
                var item = g.TryGetProperty("item", out var it) && it.ValueKind == JsonValueKind.String
                    ? it.GetString() ?? "" : "";
                var sev = g.TryGetProperty("severity", out var sv) && sv.ValueKind == JsonValueKind.String
                    ? sv.GetString() ?? "moderate" : "moderate";
                if (string.IsNullOrWhiteSpace(item)) continue;


                if (string.Equals(sev, "critical", StringComparison.OrdinalIgnoreCase))
                    antiFlagsList.Add(item);
                else
                    missingMustHaves.Add(item);
            }
        }


        var verdict = VerdictExtensions.FromScore(composite);
        var matchedJoined = matchedSkills.Count > 0 ? string.Join(", ", matchedSkills.Take(3)) : "—";
        var gapsJoined = missingMustHaves.Concat(antiFlagsList).Take(2).ToList();
        var gapsText = gapsJoined.Count > 0 ? string.Join(", ", gapsJoined) : "none";
        var reasonEn = $"{verdict.ToEnglishText()}. Strengths: {matchedJoined}. Gaps: {gapsText}.";
        var reasonUk = $"{verdict.ToUkrainianText()}. Переваги: {matchedJoined}. Брак: {gapsText}.";


        var subScores = new SubScores(0, 0, 0, 0, 0, 0, 0);

        return new ScoringResult(
            VacancyId: vacancyId,
            CvId: cvId,
            ModelVersion: Version,
            GeneratedAt: DateTime.UtcNow,
            Score: composite,
            SubScores: subScores,
            AntiFlagPenalty: 1.0,
            ReasonEn: reasonEn,
            ReasonUk: reasonUk,
            Evidence: new ScoringEvidence(matchedSkills, missingMustHaves, antiFlagsList));
    }
}
