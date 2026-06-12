using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using Application.Common.Interfaces;
using Application.Common.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Services;


public class GeminiCvNormalizationService : ICvExtractionService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ICvNormalizationPromptBuilder _promptBuilder;
    private readonly ICvNormalizationPostProcessor _postProcessor;
    private readonly ILogger<GeminiCvNormalizationService> _logger;


    private const string Model = "gemini-2.5-flash";
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";

    private readonly ILlmTracer _tracer;

    public GeminiCvNormalizationService(
        HttpClient httpClient,
        IConfiguration configuration,
        ICvNormalizationPromptBuilder promptBuilder,
        ICvNormalizationPostProcessor postProcessor,
        ILogger<GeminiCvNormalizationService> logger,
        ILlmTracer tracer)
    {
        _httpClient = httpClient;
        _apiKey = configuration["GeminiApiKey"]
            ?? throw new InvalidOperationException("GeminiApiKey is not configured");
        _promptBuilder = promptBuilder;
        _postProcessor = postProcessor;
        _logger = logger;
        _tracer = tracer;
    }

    public async Task<CvExtractionResult> ExtractAsync(
        string cvRawText,
        CancellationToken ct = default)
    {
        var truncated = cvRawText[..Math.Min(8000, cvRawText.Length)];
        var promptResult = _promptBuilder.Build(truncated);
        var prompt = promptResult.Prompt;
        var modelVersion = $"gemini-cv-normalization-{promptResult.CompositeVersion}";

        using var span = _tracer.StartSpan(
            name: "cv_normalize",
            runType: LlmRunType.LLM,
            inputs: new { domain = promptResult.DetectedDomain.ToString(), version = promptResult.CompositeVersion, model = Model, prompt });

        try
        {

            _logger.LogDebug(
                "GeminiCvNormalizationService: domain={Domain}, version={Version}, " +
                "estimatedTokens={Tokens}",
                promptResult.DetectedDomain, promptResult.CompositeVersion,
                promptResult.EstimatedInputTokens);

            var requestBody = new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {


                    temperature = 0,
                    topK = 1,
                    topP = 0.01,
                    maxOutputTokens = 8192,


                    thinkingConfig = new { thinkingBudget = 0 },


                    responseMimeType = "application/json",
                    responseSchema = BuildResponseSchema()
                }
            };

            var url = $"{BaseUrl}/{Model}:generateContent?key={_apiKey}";
            var swCall = Stopwatch.StartNew();
            var response = await _httpClient.PostAsJsonAsync(url, requestBody, ct);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(ct);
            swCall.Stop();
            var usage = ParseUsageMetadata(json);
            CostBreakdown.Track("cv_normalize", swCall.Elapsed.TotalMilliseconds, usage.Input, usage.Output);
            var rawStructured = ParseResponse(json);


            var structured = string.IsNullOrWhiteSpace(rawStructured)
                ? rawStructured
                : _postProcessor.Process(rawStructured, truncated);

            if (string.IsNullOrWhiteSpace(structured))
            {
                _logger.LogWarning(
                    "GeminiCvNormalizationService: empty result for CV of length {Len} " +
                    "(tokens: input={InTokens}, output={OutTokens}, " +
                    "thoughts={ThoughtTokens}, total={TotalTokens})",
                    cvRawText.Length,
                    usage.Input, usage.Output, usage.Thoughts, usage.Total);
                return new CvExtractionResult(string.Empty, string.Empty);
            }


            _logger.LogInformation(
                "GeminiCvNormalizationService: normalized CV — domain={Domain}, " +
                "version={Version}, input={InChars} chars → output={OutChars} chars JSON; " +
                "tokens: input={InTokens}, output={OutTokens}, " +
                "thoughts={ThoughtTokens}, total={TotalTokens}; estimated_input={EstTokens}",
                promptResult.DetectedDomain, promptResult.CompositeVersion,
                truncated.Length, structured.Length,
                usage.Input, usage.Output, usage.Thoughts, usage.Total,
                promptResult.EstimatedInputTokens);

            // Deserialize the structured JSON so LangSmith UI can render the
            // actual extracted fields (seniority, target_roles, skills, etc.)
            // rather than just metadata. Wrapped in try/catch — a malformed
            // payload must NOT throw out of the success path.
            JsonElement? extractedJson = null;
            try
            {
                using var extractedDoc = JsonDocument.Parse(structured);
                extractedJson = extractedDoc.RootElement.Clone();
            }
            catch (JsonException) { /* tracing best-effort */ }

            span.EndOk(new
            {
                domain        = promptResult.DetectedDomain.ToString(),
                version       = promptResult.CompositeVersion,
                input_tokens  = usage.Input,
                output_tokens = usage.Output,
                input_chars   = truncated.Length,
                output_chars  = structured.Length,
                extracted     = extractedJson
            });
            return new CvExtractionResult(structured, modelVersion, usage.Input, usage.Output);
        }
        catch (Exception ex)
        {
            span.EndError(ex);
            _logger.LogError(ex, "GeminiCvNormalizationService: extraction failed");
            return new CvExtractionResult(string.Empty, string.Empty);
        }
    }


    private static object BuildResponseSchema()
    {

        Dictionary<string, object> StringEnum(params string[] values) => new()
        {
            ["type"] = "STRING",
            ["enum"] = values
        };

        Dictionary<string, object> StringArray(int? maxItems = null)
        {
            var s = new Dictionary<string, object>
            {
                ["type"] = "ARRAY",
                ["items"] = new Dictionary<string, object> { ["type"] = "STRING" }
            };
            if (maxItems.HasValue) s["maxItems"] = maxItems.Value;
            return s;
        }

        var experienceItem = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["title"]           = new Dictionary<string, object> { ["type"] = "STRING" },
                ["type"]            = StringEnum("PRODUCTION", "FREELANCE", "INTERNSHIP", "PET_PROJECT", "COURSE"),
                ["duration_months"] = new Dictionary<string, object> { ["type"] = "INTEGER" },
                ["years_ago"]       = new Dictionary<string, object> { ["type"] = "INTEGER" }
            },
            ["required"]         = new[] { "title", "type", "duration_months", "years_ago" },
            ["propertyOrdering"] = new[] { "title", "type", "duration_months", "years_ago" }
        };

        var educationObj = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["degree"]          = StringEnum("bachelor", "master", "phd", "associate", "none"),
                ["field"]           = new Dictionary<string, object> { ["type"] = "STRING" },
                ["is_relevant"]     = new Dictionary<string, object> { ["type"] = "BOOLEAN" },
                ["status"]          = StringEnum("completed", "in_progress"),
                ["current_year"]    = new Dictionary<string, object> { ["type"] = "INTEGER", ["nullable"] = true },
                ["graduation_year"] = new Dictionary<string, object> { ["type"] = "INTEGER", ["nullable"] = true }
            },
            ["required"]         = new[] { "degree", "field", "is_relevant", "status" },
            ["propertyOrdering"] = new[] { "degree", "field", "is_relevant", "status", "current_year", "graduation_year" }
        };

        var languageItem = new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["language"] = new Dictionary<string, object> { ["type"] = "STRING" },
                ["level"]    = StringEnum("native", "C2", "C1", "B2", "B1", "A2", "A1")
            },
            ["required"]         = new[] { "language", "level" },
            ["propertyOrdering"] = new[] { "language", "level" }
        };

        return new Dictionary<string, object>
        {
            ["type"] = "OBJECT",
            ["properties"] = new Dictionary<string, object>
            {
                ["seniority"]         = StringEnum("junior", "middle", "senior", "lead", "intern", "not_specified"),
                ["target_roles"]      = StringArray(maxItems: 3),
                ["domain_skills"]     = StringArray(),
                ["technical_skills"]  = StringArray(),
                ["unverified_skills"] = StringArray(),
                ["experience"] = new Dictionary<string, object>
                {
                    ["type"]  = "ARRAY",
                    ["items"] = experienceItem
                },
                ["education"]     = educationObj,
                ["english_level"] = StringEnum("A1", "A2", "B1", "B2", "C1", "C2", "native", "not_specified"),
                ["languages"] = new Dictionary<string, object>
                {
                    ["type"]  = "ARRAY",
                    ["items"] = languageItem
                },
                ["has_real_product_experience"] = new Dictionary<string, object> { ["type"] = "BOOLEAN" },
                ["career_switcher"]             = new Dictionary<string, object> { ["type"] = "BOOLEAN" },
                ["confidence"] = new Dictionary<string, object>
                {
                    ["type"]        = "NUMBER",
                    ["description"] = "Self-reported certainty about the extraction in [0.0, 1.0]. " +
                                      "1.0 = detailed CV with explicit sections. 0.2 = fragmented input."
                }
            },
            ["required"] = new[]
            {
                "seniority", "target_roles", "domain_skills", "technical_skills",
                "unverified_skills", "experience", "education", "english_level",
                "languages", "has_real_product_experience", "career_switcher",
                "confidence"
            },
            ["propertyOrdering"] = new[]
            {
                "seniority", "target_roles", "domain_skills", "technical_skills",
                "unverified_skills", "experience", "education", "english_level",
                "languages", "has_real_product_experience", "career_switcher",
                "confidence"
            }
        };
    }

    private string ParseResponse(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;


            var preview = json.Length > 800 ? json[..800] + "..." : json;
            _logger.LogDebug("Gemini raw response: {Preview}", preview);


            if (!root.TryGetProperty("candidates", out var candidatesEl)
                || candidatesEl.GetArrayLength() == 0)
            {
                _logger.LogWarning(
                    "GeminiCvNormalizationService: no candidates in response. Raw: {Raw}", preview);
                return string.Empty;
            }

            var candidate = candidatesEl[0];

            if (!candidate.TryGetProperty("content", out var contentEl))
            {
                _logger.LogWarning(
                    "GeminiCvNormalizationService: no content in candidate. Raw: {Raw}", preview);
                return string.Empty;
            }

            if (!contentEl.TryGetProperty("parts", out var partsEl))
            {
                _logger.LogWarning(
                    "GeminiCvNormalizationService: no parts in content. Raw: {Raw}", preview);
                return string.Empty;
            }

            var text = string.Empty;
            foreach (var part in partsEl.EnumerateArray())
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

            if (string.IsNullOrWhiteSpace(text))
            {
                _logger.LogWarning(
                    "GeminiCvNormalizationService: all parts were thought blocks or empty. Raw: {Raw}", preview);
                return string.Empty;
            }


            text = text.Replace("```json", "").Replace("```", "").Trim();


            using var validate = JsonDocument.Parse(text);
            return text;
        }
        catch (Exception ex)
        {
            var preview = json.Length > 800 ? json[..800] + "..." : json;
            _logger.LogError(ex,
                "GeminiCvNormalizationService: failed to parse Gemini response. Raw: {Raw}", preview);
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
            {
                _logger.LogWarning(
                    "GeminiCvNormalizationService: response has no usageMetadata — " +
                    "token telemetry unavailable for this CV.");
                return GeminiTokenUsage.Empty;
            }

            return new GeminiTokenUsage(
                Input:    ReadIntOrZero(meta, "promptTokenCount"),
                Output:   ReadIntOrZero(meta, "candidatesTokenCount"),
                Thoughts: ReadIntOrZero(meta, "thoughtsTokenCount"),
                Total:    ReadIntOrZero(meta, "totalTokenCount"));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "GeminiCvNormalizationService: failed to parse usageMetadata — " +
                "continuing without token telemetry.");
            return GeminiTokenUsage.Empty;
        }
    }

    private static int ReadIntOrZero(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number
            ? p.GetInt32()
            : 0;


    private sealed record GeminiTokenUsage(int Input, int Output, int Thoughts, int Total)
    {
        public static readonly GeminiTokenUsage Empty = new(0, 0, 0, 0);
    }
}
