namespace Domain.Enums;

/// <summary>
/// Defines what kind of cabinet the user operates in.
/// <list type="bullet">
///   <item><c>Candidate</c> — default. Searches and applies to vacancies.</item>
///   <item><c>Recruiter</c> — posts vacancies and analyses candidate CVs against them.</item>
///   <item><c>Both</c> — has access to both cabinets simultaneously.</item>
/// </list>
/// Persisted as <see cref="int"/>. New users default to <see cref="Candidate"/>.
/// </summary>
public enum UserRole
{
    Candidate = 0,
    Recruiter = 1,
    Both = 2
}
