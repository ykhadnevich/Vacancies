using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Application.DTOs.Recruiter;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Recruiter.Queries.GetMyVacancies;

public sealed class GetMyVacanciesHandler
    : IRequestHandler<GetMyVacanciesQuery, IReadOnlyList<RecruiterVacancyDto>>
{
    private readonly IJobVacancyRepository _vacancies;
    private readonly ICandidateScoreRepository _scores;
    private readonly ICurrentUserService _currentUser;

    public GetMyVacanciesHandler(
        IJobVacancyRepository vacancies,
        ICandidateScoreRepository scores,
        ICurrentUserService currentUser)
    {
        _vacancies = vacancies;
        _scores = scores;
        _currentUser = currentUser;
    }

    public async Task<IReadOnlyList<RecruiterVacancyDto>> Handle(
        GetMyVacanciesQuery query, CancellationToken ct)
    {
        if (_currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var vacancies = await _vacancies.ListByOwnerAsync(userId, ct);
        var result = new List<RecruiterVacancyDto>(vacancies.Count);

        foreach (var v in vacancies)
        {
            var scored = await _scores.CountForVacancyAsync(v.Id, ct);
            result.Add(new RecruiterVacancyDto(
                Id:                    v.Id,
                Title:                 v.Title,
                Company:               v.Company,
                Location:              v.Location,
                Description:           v.Description,
                CreatedAt:             v.AggregatedAt,
                IsNormalized:          !string.IsNullOrWhiteSpace(v.VacancyAnalysisJson),
                ScoredCandidatesCount: scored));
        }

        return result;
    }
}
