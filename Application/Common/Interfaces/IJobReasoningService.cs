namespace Application.Common.Interfaces;


public interface IJobReasoningService
{


    Task<ReasoningResult> GenerateReasonAsync(
        string cvText,
        string jobTitle,
        string jobCompany,
        string jobDescription,
        float score,
        CancellationToken ct = default);


    bool SupportsFullBatch => false;
}
