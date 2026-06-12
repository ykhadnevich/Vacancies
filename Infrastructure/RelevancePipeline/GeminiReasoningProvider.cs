using Application.Common.Enums;
using Application.Common.Interfaces;
using Infrastructure.RelevancePipeline.Prompts;
using Microsoft.Extensions.Logging;


namespace Infrastructure.RelevancePipeline;


public class GeminiReasoningProvider : IJobReasoningService
{
    private readonly IGeminiScoringService _gemini;
    private readonly ILogger<GeminiReasoningProvider> _logger;
    private readonly IReasoningContext _reasoningContext;

    public GeminiReasoningProvider(
        IGeminiScoringService gemini,
        ILogger<GeminiReasoningProvider> logger,
        IReasoningContext reasoningContext)
    {
        _gemini = gemini;
        _logger = logger;
        _reasoningContext = reasoningContext;
    }


    public static string BuildModelVersion(ScoringModelType model) =>
#pragma warning disable CS0618
        $"gemini-{model.ToString().ToLowerInvariant()}-{ScoringPrompt.Version}";
#pragma warning restore CS0618


    public bool SupportsFullBatch => true;

    public async Task<ReasoningResult> GenerateReasonAsync(
        string cvText,
        string jobTitle,
        string jobCompany,
        string jobDescription,
        float score,
        CancellationToken ct = default)
    {
        var jobId = Guid.NewGuid();

        var results = await _gemini.ScoreJobsAsync(
            new[] { (jobId, jobTitle, jobCompany, (string?)jobDescription) },
            cvText,
            ct);

        var result = results.FirstOrDefault();
        if (result is null || string.IsNullOrEmpty(result.Reason))
        {
            _logger.LogWarning("GeminiReasoningProvider: empty reason for [{Title}] @ [{Company}]",
                jobTitle, jobCompany);
            return new ReasoningResult(string.Empty, "gemini-empty");
        }


        return new ReasoningResult(
            result.Reason,
            BuildModelVersion(_reasoningContext.ScoringModel),
            result.Score,
            result.InputTokens,
            result.OutputTokens);
    }
}
