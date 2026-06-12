using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline;


public class GroqReasoningProvider : IJobReasoningService
{
    private readonly IReasoningService _groq;

    public GroqReasoningProvider(IReasoningService groq)
    {
        _groq = groq;
    }

    public Task<ReasoningResult> GenerateReasonAsync(
        string cvText,
        string jobTitle,
        string jobCompany,
        string jobDescription,
        float score,
        CancellationToken ct = default)
        => _groq.GenerateReasonAsync(cvText, jobTitle, jobDescription, score, ct);
}
