using Application.DTOs;

namespace Application.Common.Interfaces;

public interface ILlmRerankService
{
    Task<IReadOnlyList<RankedJobDto>> RerankAsync(
        IReadOnlyList<JobVacancyDto> jobs,
        string userProfileSummary,
        CancellationToken ct = default);
}
