using Domain.Enums;

namespace Domain.Scoring;


/// <summary>
/// Single source of truth for seniority year boundaries used by both
/// SeniorityMatchCalculator and ExperienceMatchCalculator.
///
/// Previously these classes had inconsistent definitions of "senior"
/// (one used years &lt;= 6, the other implied senior = 5 years).
/// </summary>
public static class SeniorityBoundaries
{

    public static SeniorityLevel FromYears(int years) => years switch
    {
        <= 0 => SeniorityLevel.NotSpecified,
        <= 1 => SeniorityLevel.Junior,
        <= 3 => SeniorityLevel.Middle,
        <= 5 => SeniorityLevel.Senior,
        _    => SeniorityLevel.Lead,
    };


    /// <remarks>
    /// CAVEAT: <see cref="SeniorityLevel.Internship"/> and <see cref="SeniorityLevel.NotSpecified"/>
    /// both return 0 — they collide in the integer space. Round-tripping via
    /// <see cref="FromYears(int)"/> from this value yields <see cref="SeniorityLevel.NotSpecified"/>,
    /// NOT the original input. Callers requiring round-trip stability for Internship should
    /// track the original enum value separately.
    /// </remarks>
    public static int MinYears(SeniorityLevel level) => level switch
    {
        SeniorityLevel.Internship   => 0,
        SeniorityLevel.Junior       => 1,
        SeniorityLevel.Middle       => 3,
        SeniorityLevel.Senior       => 5,
        SeniorityLevel.Lead         => 6,
        SeniorityLevel.NotSpecified => 0,
        _                           => 0,
    };


    public static string ToCanonicalString(SeniorityLevel level) => level switch
    {
        SeniorityLevel.Internship   => "intern",
        SeniorityLevel.Junior       => "junior",
        SeniorityLevel.Middle       => "middle",
        SeniorityLevel.Senior       => "senior",
        SeniorityLevel.Lead         => "lead",
        SeniorityLevel.NotSpecified => "not_specified",
        _                           => "not_specified",
    };


    public static SeniorityLevel FromString(string? raw) => raw?.Trim().ToLowerInvariant() switch
    {
        "intern" or "internship" or "trainee"           => SeniorityLevel.Internship,
        "junior" or "jr"                                => SeniorityLevel.Junior,
        "middle" or "mid"                               => SeniorityLevel.Middle,
        "senior" or "sr"                                => SeniorityLevel.Senior,
        "lead" or "principal" or "staff" or "head" or "chief" => SeniorityLevel.Lead,
        _                                               => SeniorityLevel.NotSpecified,
    };
}
