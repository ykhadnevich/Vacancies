using Application.Common.Authorization;
using Application.Common.Exceptions;
using Application.Common.Interfaces;
using Domain.Enums;
using Domain.Interfaces.Repositories;
using MediatR;

namespace Application.Common.Behaviors;

/// <summary>
/// Runs before any MediatR request that implements <see cref="IRequireRecruiterRole"/>.
/// Loads the current user and confirms <c>Role</c> is <see cref="UserRole.Recruiter"/>
/// or <see cref="UserRole.Both"/>. Failures surface as <see cref="ForbiddenAccessException"/>.
/// </summary>
public sealed class RequireRecruiterBehavior<TRequest, TResponse>
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    private readonly ICurrentUserService _currentUser;
    private readonly IUserProfileRepository _users;

    public RequireRecruiterBehavior(
        ICurrentUserService currentUser,
        IUserProfileRepository users)
    {
        _currentUser = currentUser;
        _users = users;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken ct)
    {
        if (request is not IRequireRecruiterRole)
            return await next(ct);

        if (!_currentUser.IsAuthenticated || _currentUser.UserId is not Guid userId)
            throw new ForbiddenAccessException("Authentication required.");

        var profile = await _users.GetByIdAsync(userId, ct);
        if (profile is null)
            throw new ForbiddenAccessException("User profile not found.");

        if (profile.Role != UserRole.Recruiter && profile.Role != UserRole.Both)
            throw new ForbiddenAccessException(
                "This endpoint is available only to users with the Recruiter role.");

        return await next(ct);
    }
}
