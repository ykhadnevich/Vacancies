using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;


public class GeminiVacancyNormalizationService : IVacancyExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly IVacancyNormalizationPromptBuilder _promptBuilder;
    private readonly IVacancyNormalizationPostProcessor _postProcessor;
    private readonly ILogger<GeminiVacancyNormalizationService> _logger;

    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    public GeminiVacancyNormalizationService(
        HttpClient httpClient,
        IConfiguration configuration,
        IVacancyNormalizationPromptBuilder promptBuilder,
        IVacancyNormalizationPostProcessor postProcessor,
        ILogger<GeminiVacancyNormalizationService> logger)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _promptBuilder = promptBuilder;
        _postProcessor = postProcessor;
        _logger = logger;
    }

    public async Task<VacancyExtractionResult> ExtractAsync(
        string vacancyRawText,
        CancellationToken ct = default)
    {
        try
        {


            var truncated = vacancyRawText[..Math.Min(12000, vacancyRawText.Length)];
            var promptResult = _promptBuilder.Build(truncated);
            var prompt = promptResult.Prompt;
            var modelVersion = $"gemini-vac-normalization-{promptResult.CompositeVersion}";

            _logger.LogDebug(
                "GeminiVacancyNormalizationService: domain={Domain}, version={Version}, " +
                "estimatedTokens={Tokens}",
                promptResult.DetectedDomain, promptResult.CompositeVersion,
                promptResult.EstimatedInputTokens);

            var requestBody = new
            {
                contents = new[] { new { parts = new[] { new { text = prompt } } } },
                generationConfig = new
                {


                    temperature = 0.1,
                    topP = 0.95,
                    maxOutputTokens = 8192,
                    thinkingConfig = new { thinkingBudget = 0 },
                    responseMimeType = "application/json",
                    responseSchema = BuildResponseSchema()
                }
            };


            using var perCallCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            perCallCts.CancelAfter(TimeSpan.FromSeconds(20));

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var swCall = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, perCallCts.Token);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(perCallCts.Token);
            swCall.Stop();
            var usage = ParseUsageMetadata(json);
            CostBreakdown.Track("vacancy_normalize", swCall.Elapsed.TotalMilliseconds, usage.Input, usage.Output);
            var rawStructured = ParseResponse(json);

            var structured = string.IsNullOrWhiteSpace(rawStructured)
                ? rawStructured
                : _postProcessor.Process(rawStructured, truncated);

            if (string.IsNullOrWhiteSpace(structured))
            {
                _logger.LogWarning(
                    "GeminiVacancyNormalizationService: empty result for vacancy of length {Len} " +
                    "(tokens: input={InTokens}, output={OutTokens})",
                    vacancyRawText.Length, usage.Input, usage.Output);
                return new VacancyExtractionResult(string.Empty, string.Empty);
            }

            _logger.LogInformation(
                "GeminiVacancyNormalizationService: normalized vacancy — domain={Domain}, " +
                "version={Version}, input={InChars}→{OutChars}; " +
                "tokens: input={InTokens}, output={OutTokens}, total={TotalTokens}",
                promptResult.DetectedDomain, promptResult.CompositeVersion,
                truncated.Length, structured.Length,
                usage.Input, usage.Output, usage.Total);

            return new VacancyExtractionResult(structured, modelVersion, usage.Input, usage.Output);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "GeminiVacancyNormalizationService: extraction failed");
            return new VacancyExtractionResult(string.Empty, string.Empty);
        }
    }


    private static object BuildResponseSchema()
    {
        Dictionary<string, object> StringEnum(params string[] values) => new()
        {
            ["type"] = "STRING",
            ["enum"] = values
        };

        Dictionary<string, object> StringArray() => new()
        {
            ["type"] = "ARRAY",
            ["items"] = new Dictionary<string, object> { ["type"] = "STRING" }
        };

        var bilingualText = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["en"] = new Dictionary<string, object> { ["type"] = "STRING" },
                ["uk"] = new Dictionary<string, object> { ["type"] = "STRING" }
            },
            ["required"] = new[] { "en", "uk" },
            ["propertyOrdering"] = new[] { "en", "uk" }
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
            ["required"] = new[] { "remote", "hybrid" },
            ["propertyOrdering"] = new[] { "city_en", "city_uk", "remote", "hybrid" }
        };

        return new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["source_language"]      = StringEnum("uk", "en", "mixed", "unknown"),
                ["role_title"]           = bilingualText,
                ["role_title_raw"]       = new Dictionary<string, object> { ["type"] = "STRING" },
                ["seniority_required"]   = StringEnum("junior", "middle", "senior", "lead", "intern", "not_specified"),
                ["must_have_skills"]     = StringArray(),
                ["nice_to_have_skills"]  = StringArray(),
                ["min_years_experience"] = new Dictionary<string, object> { ["type"] = "INTEGER", ["nullable"] = true },
                ["education_required"]   = StringEnum("none", "bachelor", "master", "phd", "not_specified"),
                ["english_required"]     = StringEnum("A1", "A2", "B1", "B2", "C1", "C2", "native", "not_specified"),
                ["location"]             = locationObj,
                ["domain_context"]       = bilingualText,
                ["anti_requirements"]    = StringArray()
            },
            ["required"] = new[]
            {
                "source_language", "role_title", "role_title_raw",
                "seniority_required", "must_have_skills", "nice_to_have_skills",
                "education_required", "english_required",
                "location", "domain_context", "anti_requirements"
            },
            ["propertyOrdering"] = new[]
            {
                "source_language", "role_title", "role_title_raw",
                "seniority_required",
                "must_have_skills", "nice_to_have_skills",
                "min_years_experience", "education_required", "english_required",
                "location", "domain_context", "anti_requirements"
            }
        };
    }


    private string ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (!root.TryGetProperty("candidates", out var candidates)
                || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("GeminiVacancyNormalizationService: no candidates in response");
                return string.Empty;
            }

            var first = candidates[0];
            if (!first.TryGetProperty("content", out var content)
                || !content.TryGetProperty("parts", out var parts)
                || parts.GetArrayLength() == 0)
            {
                _logger.LogWarning("GeminiVacancyNormalizationService: no content.parts");
                return string.Empty;
            }

            string text = string.Empty;
            foreach (var part in parts.EnumerateArray())
            {

                if (part.TryGetProperty("thought", out var t) && t.GetBoolean())
                    continue;
                if (part.TryGetProperty("text", out var txt))
                {
                    text = txt.GetString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(text)) break;
                }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "GeminiVacancyNormalizationService: all parts were empty/thought");
                return string.Empty;
            }

            text = text.Replace("```json", "").Replace("```", "").Trim();


            using var validate = JsonDocument.Parse(text);
            return text;
        }
        catch (Exception ex)
        {
            var preview = json.Length > 600 ? json[..600] + "..." : json;
            _logger.LogError(ex,
                "GeminiVacancyNormalizationService: failed to parse response. Raw: {Raw}",
                preview);
            return string.Empty;
        }
    }

    private GeminiTokenUsage ParseUsageMetadata(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("usageMetadata", out var meta))
                return GeminiTokenUsage.Empty;

            return new GeminiTokenUsage(
                Input:    ReadIntOrZero(meta, "promptTokenCount"),
                Output:   ReadIntOrZero(meta, "candidatesTokenCount"),
                Thoughts: ReadIntOrZero(meta, "thoughtsTokenCount"),
                Total:    ReadIntOrZero(meta, "totalTokenCount"));
        }
        catch
        {
            return GeminiTokenUsage.Empty;
        }
    }

    private static int ReadIntOrZero(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32() : 0;

    private sealed record GeminiTokenUsage(int Input, int Output, int Thoughts, int Total)
    {
        public static readonly GeminiTokenUsage Empty = new(0, 0, 0, 0);
    }
}
