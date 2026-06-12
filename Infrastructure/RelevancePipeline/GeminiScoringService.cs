using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Application.Common.Enums;
using Application.Common.Interfaces;
using Application.Common.Scoring;
using Infrastructure.RelevancePipeline.Prompts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.RelevancePipeline;


public class GeminiScoringService : IGeminiScoringService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<GeminiScoringService> _logger;
    private readonly IScoringPromptBuilder _promptBuilder;
    private readonly IReasoningContext _reasoningContext;

    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiScoringService(
        HttpClient httpClient,
        IConfiguration config,
        ILogger<GeminiScoringService> logger,
        IScoringPromptBuilder promptBuilder,
        IReasoningContext reasoningContext)
    {
        _httpClient       = httpClient;
        _apiKey           = config["GeminiApiKey"]
                            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _logger           = logger;
        _promptBuilder    = promptBuilder;
        _reasoningContext = reasoningContext;
    }


    private static string ResolveModelName(ScoringModelType model) => model switch
    {
        ScoringModelType.FlashLite     => "gemini-2.5-flash-lite",
        ScoringModelType.FlashThinking => "gemini-2.5-flash",
        ScoringModelType.Flash         => "gemini-2.5-flash",
        _                              => "gemini-2.5-flash",
    };


    private static int ResolveThinkingBudget(ScoringModelType model) => model switch
    {
        ScoringModelType.FlashThinking => 4096,
        _                              => 0,
    };

    public async Task<IReadOnlyList<GeminiJobScore>> ScoreJobsAsync(
        IReadOnlyList<(Guid Id, string Title, string Company, string? Description)> jobs,
        string userProfileText,
        CancellationToken ct = default)
    {
        try
        {


            var modelChoice    = _reasoningContext.ScoringModel;
            var modelName      = ResolveModelName(modelChoice);
            var thinkingBudget = ResolveThinkingBudget(modelChoice);

            _logger.LogInformation(
                "Gemini scoring batch — model: {Model} ({Endpoint}, thinkingBudget={Thinking}) | jobs: {Count}",
                modelChoice, modelName, thinkingBudget, jobs.Count);

            var roleYears = ComputeRoleWeightedYearsForPrompt(userProfileText);

            var tasks = jobs.Select(job =>
                ScoreSingleJobAsync(job, userProfileText, roleYears, modelName, thinkingBudget, ct));
            var results = await Task.WhenAll(tasks);

            return results.ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gemini batch scoring failed: {Message}", ex.Message);
            return jobs.Select(j => new GeminiJobScore(j.Id, 50f, string.Empty)).ToList();
        }
    }

    private async Task<GeminiJobScore> ScoreSingleJobAsync(
        (Guid Id, string Title, string Company, string? Description) job,
        string userProfileText,
        RoleWeightedYears? roleYears,
        string modelName,
        int thinkingBudget,
        CancellationToken ct)
    {
        try
        {
            var desc = job.Description?[..Math.Min(2000, job.Description?.Length ?? 0)] ?? "немає опису";


            var promptCtx = new ScoringPromptContext(
                cvText:         userProfileText,
                jobTitle:       job.Title,
                jobCompany:     job.Company,
                jobDescription: desc,
                roleYears:      roleYears);
            var prompt = _promptBuilder.Build(promptCtx).Prompt;

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature      = 0.1,
                    maxOutputTokens  = 1024,
                    thinkingConfig   = new { thinkingBudget }
                }
            };

            var url      = $"{BaseUrl}/{modelName}:generateContent?key={_apiKey}";
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            return ParseResponse(json, job.Id);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Gemini scoring failed for [{Title}]: {Message}", job.Title, ex.Message);
            return new GeminiJobScore(job.Id, 50f, string.Empty);
        }
    }

    private GeminiJobScore ParseResponse(string json, Guid jobId)
    {
        try
        {
            using var doc  = JsonDocument.Parse(json);
            var root       = doc.RootElement;

            var parts = root
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts");

            var text = "{}";
            foreach (var part in parts.EnumerateArray())
            {

                if (part.TryGetProperty("thought", out var thoughtProp) && thoughtProp.GetBoolean())
                    continue;

                if (part.TryGetProperty("text", out var t))
                {
                    var partText = t.GetString() ?? "";
                    if (!string.IsNullOrWhiteSpace(partText))
                        text = partText;
                }
            }

            text = text.Replace("```json", "").Replace("```", "").Trim();

            var inputTokens  = 0;
            var outputTokens = 0;
            if (root.TryGetProperty("usageMetadata", out var usage))
            {
                if (usage.TryGetProperty("promptTokenCount",     out var p)) inputTokens  = p.GetInt32();
                if (usage.TryGetProperty("candidatesTokenCount", out var c)) outputTokens = c.GetInt32();
            }

            using var resultDoc = JsonDocument.Parse(text);
            var r = resultDoc.RootElement;

            var score   = r.TryGetProperty("score",   out var sv) ? sv.GetSingle() : 50f;
            var verdict = r.TryGetProperty("verdict", out var vv) ? vv.GetString() ?? "partial_fit" : "partial_fit";
            var matched = r.TryGetProperty("matched", out var mv) ? mv.GetString() ?? "none" : "none";
            var gaps    = ParseGaps(r);

            score = Math.Clamp(score, 0, 100);

            var reason = $"Verdict: {verdict}\nMatched: {matched}\nGaps: {gaps}";

            return new GeminiJobScore(jobId, score, reason, inputTokens, outputTokens);
        }
        catch (Exception ex)
        {
            var preview = json.Length > 500 ? json[..500] + "..." : json;
            _logger.LogError(ex, "ParseResponse failed. Raw response preview: {Preview}", preview);
            return new GeminiJobScore(jobId, 50f, string.Empty);
        }
    }


    private static readonly Regex TrailingSeverityRx = new(
        @"\s*\((critical|moderate|minor)\)\s*$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);


    private static string ParseGaps(JsonElement root)
    {
        if (!root.TryGetProperty("gaps", out var gv))
            return "none";


        if (gv.ValueKind == JsonValueKind.Array)
        {
            var items = new List<string>();
            foreach (var entry in gv.EnumerateArray())
            {
                if (entry.ValueKind != JsonValueKind.Object) continue;

                var item = entry.TryGetProperty("item", out var iv)
                    ? iv.GetString()?.Trim()
                    : null;
                var severityRaw = entry.TryGetProperty("severity", out var sev)
                    ? sev.GetString()?.Trim().ToLowerInvariant()
                    : null;

                if (string.IsNullOrEmpty(item)) continue;


                item = TrailingSeverityRx.Replace(item, "").Trim();
                if (item.Length == 0) continue;

                var severity = severityRaw switch
                {
                    "critical" => "critical",
                    "moderate" => "moderate",
                    "minor"    => "minor",
                    _          => "moderate"
                };
                items.Add($"{item} ({severity})");
            }
            return items.Count == 0 ? "none" : string.Join(", ", items);
        }


        if (gv.ValueKind == JsonValueKind.String)
            return gv.GetString() ?? "none";

        return "none";
    }


    private static RoleWeightedYears? ComputeRoleWeightedYearsForPrompt(string userProfileText)
    {
        if (!userProfileText.TrimStart().StartsWith("{"))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(userProfileText);
            var root = doc.RootElement;

            if (!root.TryGetProperty("experience", out var experienceEl))
                return null;

            var pmPo = 0.0; var pmm = 0.0; var ba = 0.0; var pm = 0.0;
            var dev  = 0.0; var da  = 0.0; var ds = 0.0; var mkt = 0.0;

            foreach (var entry in experienceEl.EnumerateArray())
            {
                var title = entry.TryGetProperty("title", out var t) && t.ValueKind == JsonValueKind.String
                    ? t.GetString() ?? "" : "";
                var type  = entry.TryGetProperty("type",  out var tp) && tp.ValueKind == JsonValueKind.String
                    ? tp.GetString() ?? "" : "";

                if (type.Equals("COURSE", StringComparison.OrdinalIgnoreCase)) continue;

                var months = entry.TryGetProperty("duration_months", out var dm) && dm.ValueKind == JsonValueKind.Number
                    ? dm.GetDouble() : 0.0;

                double mul = type.ToUpperInvariant() switch
                {
                    "PRODUCTION"  => 1.0,
                    "FREELANCE"   => 0.7,
                    "INTERNSHIP"  => 0.5,
                    "PET_PROJECT" => 0.2,
                    _             => 1.0
                };

                var tl = title.ToLowerInvariant();
                if      (tl.Contains("product marketing") || tl.Contains("growth marketing") || tl.Contains("growth manager")) pmm  += months * mul;
                else if (tl.Contains("project manager")   || tl.Contains("program manager"))                                  pm   += months * mul;
                else if (tl.Contains("product manager")   || tl.Contains("product owner") ||
                         tl.Contains("head of product")   || tl.Contains("product lead"))                                    pmPo += months * mul;
                else if (tl.Contains("business analyst")  || tl.Contains("system analyst"))                                   ba   += months * mul;
                else if (tl.Contains("data analyst")      || tl.Contains("data scientist"))                                   da   += months * mul;
                else if (tl.Contains("developer")         || tl.Contains("engineer") ||
                         tl.Contains("software")          || tl.Contains("backend") ||
                         tl.Contains("frontend")          || tl.Contains("fullstack"))                                        dev  += months * mul;
                else if (tl.Contains("designer")          || tl.Contains("ux") || tl.Contains("ui"))                         ds   += months * mul;
                else if (tl.Contains("marketing"))                                                                             mkt  += months * mul;
            }

            return new RoleWeightedYears(
                Math.Round(pmPo / 12, 1), Math.Round(pmm / 12, 1),
                Math.Round(ba   / 12, 1), Math.Round(pm  / 12, 1),
                Math.Round(dev  / 12, 1), Math.Round(da  / 12, 1),
                Math.Round(ds   / 12, 1), Math.Round(mkt / 12, 1));
        }
        catch
        {
            return null;
        }
    }
}
