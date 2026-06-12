using Application.Common.Authorization;
using Application.DTOs.Recruiter;
using MediatR;

namespace Application.Recruiter.Queries.GetMyCandidateLists;

public sealed record GetMyCandidateListsQuery()
    : IRequest<IReadOnlyList<CandidateListDto>>, IRequireRecruiterRole;
