using Application.Common.Authorization;
using Application.DTOs.Recruiter;
using MediatR;

namespace Application.Recruiter.Queries.GetMyVacancies;

public sealed record GetMyVacanciesQuery()
    : IRequest<IReadOnlyList<RecruiterVacancyDto>>, IRequireRecruiterRole;
