namespace Application.Common.Authorization;

/// <summary>
/// Marker interface for MediatR requests that require the caller to have the
/// Recruiter or Both <c>UserRole</c>. The <c>RequireRecruiterBehavior</c>
/// pipeline behavior enforces this once for every implementing request,
/// keeping the role check out of individual handlers.
/// </summary>
public interface IRequireRecruiterRole
{
}
