namespace Application.Common.Interfaces;


public interface IEvalDataSource
{


    Task<string?> GetCvSummaryAsync(string cvId, CancellationToken ct = default);


    Task<string?> GetVacancyAnalysisAsync(Guid vacancyId, CancellationToken ct = default);
}
