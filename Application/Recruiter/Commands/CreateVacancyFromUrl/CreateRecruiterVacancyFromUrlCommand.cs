using Application.Common.Auditing;
using Application.Common.Authorization;
using Application.Recruiter.Commands.CreateVacancy;
using MediatR;

namespace Application.Recruiter.Commands.CreateVacancyFromUrl;

public sealed record CreateRecruiterVacancyFromUrlCommand(string Url)
    : IRequest<CreateRecruiterVacancyResult>, IRequireRecruiterRole, IAuditableRequest
{
    public string AuditAction => "CreateRecruiterVacancyFromUrl";
}
