using Application.Common.Auditing;
using Application.DTOs.Eval;
using MediatR;

namespace Application.Eval.Commands.ScoreSinglePair;


public sealed record ScoreSinglePairCommand(string CvId, Guid VacancyId)
    : IRequest<EvalPairResultDto>, IAuditableRequest, IAuditableEntity
{
    public string AuditAction     => "ScoreSinglePair";
    public string AuditEntityType => "Vacancy";
    public Guid   AuditEntityId   => VacancyId;
}
