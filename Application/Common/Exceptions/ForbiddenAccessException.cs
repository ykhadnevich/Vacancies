namespace Application.Common.Exceptions;

/// <summary>
/// Thrown by Recruiter-cabinet pipeline behaviors when the caller fails an
/// authorization check (wrong role, or accessing a resource they do not own).
/// The API layer translates this to HTTP 403.
/// </summary>
public sealed class ForbiddenAccessException : Exception
{
    public ForbiddenAccessException(string message) : base(message) { }
}
