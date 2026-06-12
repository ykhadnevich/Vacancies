using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Application.Common.Interfaces;


public interface IBatchedVacancyExtractionService
{
    Task<IReadOnlyDictionary<Guid, VacancyExtractionResult>> ExtractBatchAsync(
        IReadOnlyList<BatchedVacancyExtractionRequest> requests,
        CancellationToken ct = default);


    string Version { get; }
}


public sealed record BatchedVacancyExtractionRequest(
    Guid VacancyId,
    string VacancyRawText);
