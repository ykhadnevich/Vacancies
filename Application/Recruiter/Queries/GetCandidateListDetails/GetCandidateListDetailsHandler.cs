using Application.DTOs.Recruiter;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Recruiter.Queries.GetCandidateListDetails;

public sealed class GetCandidateListDetailsHandler
    : IRequestHandler<GetCandidateListDetailsQuery, IReadOnlyList<CandidateInListDto>>
{
    private readonly IRecruiterCandidateRepository _candidates;

    public GetCandidateListDetailsHandler(IRecruiterCandidateRepository candidates)
    {
        _candidates = candidates;
    }

    public async Task<IReadOnlyList<CandidateInListDto>> Handle(
        GetCandidateListDetailsQuery query, CancellationToken ct)
    {
        var members = await _candidates.ListByListAsync(query.CandidateListId, ct);
        return members
            .Select(c => new CandidateInListDto(
                Id:                  c.Id,
                CandidateName:       c.CandidateName,
                NormalizationStatus: c.Status.ToString(),
                LastError:           c.LastError,
                AddedAt:             c.AddedAt))
            .ToList();
    }
}
