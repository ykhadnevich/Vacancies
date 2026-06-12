using Application.Common.Auditing;
using Application.Common.Authorization;
using MediatR;

namespace Application.Recruiter.Commands.CreateVacancy;

public sealed record CreateRecruiterVacancyCommand(
    string Title,
    string Company,
    string RawDescription,
    string? Location = null)
    : IRequest<CreateRecruiterVacancyResult>, IRequireRecruiterRole, IAuditableRequest, IAuditablePayload
{
    public string AuditAction => "CreateRecruiterVacancy";

    // RawDescription is multi-KB free-form text from scrapers — may include PII; record metadata only.
    public IReadOnlyDictionary<string, object?>? BuildAuditPayload() => new Dictionary<string, object?>
    {
        ["title"]              = Title,
        ["company"]            = Company,
        ["location"]           = Location,
        ["rawDescriptionSize"] = RawDescription?.Length ?? 0,
    };
}

public sealed record CreateRecruiterVacancyResult(
    Guid VacancyId,
    bool NormalizationSucceeded,
    string? NormalizationError);
