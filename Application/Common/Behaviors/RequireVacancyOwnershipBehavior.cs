using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Common.Behaviors;

/// <summary>
/// Verifies <see cref="IRequireVacancyOwnership.VacancyId"/> belongs to the current user
/// before the handler runs. Treats a missing or unowned vacancy as 403 to avoid leaking
/// the existence of vacancies the recruiter does not own.
/// </summary>
public sealed class RequireVacancyOwnershipBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly IJobVacancyRepository _vacancies;

    public RequireVacancyOwnershipBehavior(
        ICurrentUserService currentUser,
        IJobVacancyRepository vacancies)
    {
        _currentUser = currentUser;
        _vacancies = vacancies;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not IRequireVacancyOwnership owned)
            return await next(ct);

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var vacancy = await _vacancies.GetByIdAsync(owned.VacancyId, ct);
        if (vacancy is null || vacancy.OwnerUserId != userId)
            throw new ForbiddenAccessException(
                $"You do not have access to vacancy {owned.VacancyId}.");

        return await next(ct);
    }
}
