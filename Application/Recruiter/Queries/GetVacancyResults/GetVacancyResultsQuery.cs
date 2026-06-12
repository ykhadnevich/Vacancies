using Application.Common.Authorization;
using Application.DTOs.Recruiter;
using MediatR;

namespace Application.Recruiter.Queries.GetVacancyResults;

public sealed record GetVacancyResultsQuery(Guid VacancyId, Guid CandidateListId)
    : IRequest<IReadOnlyList<CandidateAnalysisResultDto>>,
      IRequireRecruiterRole,
      IRequireVacancyOwnership,
      IRequireCandidateListOwnership;
