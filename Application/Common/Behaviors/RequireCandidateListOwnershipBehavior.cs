using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Common.Behaviors;

/// <summary>
/// Verifies <see cref="IRequireCandidateListOwnership.CandidateListId"/> belongs to the
/// current user before the handler runs.
/// </summary>
public sealed class RequireCandidateListOwnershipBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly ICandidateListRepository _lists;

    public RequireCandidateListOwnershipBehavior(
        ICurrentUserService currentUser,
        ICandidateListRepository lists)
    {
        _currentUser = currentUser;
        _lists = lists;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not IRequireCandidateListOwnership owned)
            return await next(ct);

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var list = await _lists.GetByIdAsync(owned.CandidateListId, ct);
        if (list is null || list.RecruiterUserId != userId)
            throw new ForbiddenAccessException(
                $"You do not have access to candidate list {owned.CandidateListId}.");

        return await next(ct);
    }
}
