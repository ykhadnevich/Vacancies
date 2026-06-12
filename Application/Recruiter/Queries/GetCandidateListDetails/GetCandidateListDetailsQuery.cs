using Application.Common.Authorization;
using Application.DTOs.Recruiter;
using MediatR;

namespace Application.Recruiter.Queries.GetCandidateListDetails;

public sealed record GetCandidateListDetailsQuery(Guid CandidateListId)
    : IRequest<IReadOnlyList<CandidateInListDto>>,
      IRequireRecruiterRole,
      IRequireCandidateListOwnership;
