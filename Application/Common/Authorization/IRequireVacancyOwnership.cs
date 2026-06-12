namespace Application.Common.Authorization;

/// <summary>
/// Marker for MediatR requests that operate on a specific recruiter-owned vacancy.
/// The <c>RequireVacancyOwnershipBehavior</c> pipeline behavior fetches the vacancy,
/// confirms <c>OwnerUserId == CurrentUser.UserId</c>, and short-circuits with
/// <c>ForbiddenAccessException</c> if it does not.
/// </summary>
public interface IRequireVacancyOwnership
{
    Guid VacancyId { get; }
}
