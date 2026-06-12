using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.DTOs.Recruiter;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Recruiter.Queries.GetMyCandidateLists;

public sealed class GetMyCandidateListsHandler
    : IRequestHandler<GetMyCandidateListsQuery, IReadOnlyList<CandidateListDto>>
{
    private readonly ICandidateListRepository _lists;
    private readonly IRecruiterCandidateRepository _candidates;
    private readonly ICurrentUserService _currentUser;

    public GetMyCandidateListsHandler(
        ICandidateListRepository lists,
        IRecruiterCandidateRepository candidates,
        ICurrentUserService currentUser)
    {
        _lists = lists;
        _candidates = candidates;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<CandidateListDto>> Handle(
        GetMyCandidateListsQuery query, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var lists = await _lists.ListByRecruiterAsync(userId, ct);
        var result = new List<CandidateListDto>(lists.Count);

        foreach (var list in lists)
        {
            var members = await _candidates.ListByListAsync(list.Id, ct);
            var normalized = members.Count(c => c.Status == CandidateNormalizationStatus.Normalized);
            var failed = members.Count(c => c.Status == CandidateNormalizationStatus.Failed);

            result.Add(new CandidateListDto(
                Id:                    list.Id,
                Name:                  list.Name,
                Description:           list.Description,
                CreatedAt:             list.CreatedAt,
                TotalCandidates:       members.Count,
                NormalizedCandidates:  normalized,
                FailedCandidates:      failed));
        }

        return result;
    }
}
