using Application.Common.Interfaces;

namespace Infrastructure.RelevancePipeline;


public class NoOpReasoningProvider : IJobReasoningService
{
    public Task<ReasoningResult> GenerateReasonAsync(
        string cvText,
        string jobTitle,
        string jobCompany,
        string jobDescription,
        float score,
        CancellationToken ct = default)
        => Task.FromResult(new ReasoningResult(string.Empty, "none"));
}
