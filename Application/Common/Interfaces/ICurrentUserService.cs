namespace Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid? UserId { get; }
    bool IsAuthenticated { get; }

    // Resolved via X-Forwarded-For when UseForwardedHeaders is active.
    string? IpAddress { get; }
    string? UserAgent { get; }
}
