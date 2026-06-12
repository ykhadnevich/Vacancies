using Domain.Scoring;

namespace Domain.Entities;


public sealed class ScoringCacheEntry
{

    public string CvHash { get; private set; } = string.Empty;


    public Guid VacancyId { get; private set; }


    public string ScoringVersion { get; private set; } = string.Empty;


    public double? JudgeScore { get; private set; }


    public Verdict? JudgeVerdict { get; private set; }


    public string? StrengthsEn      { get; private set; }
    public string? StrengthsUk      { get; private set; }
    public string? GapsEn           { get; private set; }
    public string? GapsUk           { get; private set; }
    public string? RecommendationEn { get; private set; }
    public string? RecommendationUk { get; private set; }


    /// <summary>
    /// Serialised full <see cref="Domain.Scoring.ScoringResult"/> for the Mono
    /// engine path. Null on Linear+Judge rows. ScoringVersion = Mono prompt
    /// version when this is non-null.
    /// </summary>
    public string? MonoResultJson { get; private set; }

    public DateTime CreatedAt { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    private ScoringCacheEntry() { }


    public static ScoringCacheEntry FromJudge(
        string cvHash,
        Guid vacancyId,
        string scoringVersion,
        double judgeScore,
        Verdict judgeVerdict)
    {
        ValidateKey(cvHash, scoringVersion);
        var now = DateTime.UtcNow;
        return new ScoringCacheEntry
        {
            CvHash         = cvHash,
            VacancyId      = vacancyId,
            ScoringVersion = scoringVersion,
            JudgeScore     = judgeScore,
            JudgeVerdict   = judgeVerdict,
            CreatedAt      = now,
            UpdatedAt      = now,
        };
    }


    public void WriteJudge(double judgeScore, Verdict judgeVerdict)
    {
        JudgeScore   = judgeScore;
        JudgeVerdict = judgeVerdict;
        UpdatedAt    = DateTime.UtcNow;
    }


    public void WriteReason(
        string strengthsEn, string strengthsUk,
        string gapsEn,      string gapsUk,
        string recommendationEn, string recommendationUk)
    {
        if (string.IsNullOrWhiteSpace(strengthsEn) || string.IsNullOrWhiteSpace(strengthsUk)
            || string.IsNullOrWhiteSpace(gapsEn) || string.IsNullOrWhiteSpace(gapsUk)
            || string.IsNullOrWhiteSpace(recommendationEn) || string.IsNullOrWhiteSpace(recommendationUk))
            throw new ArgumentException("All reason sections must be non-empty");
        StrengthsEn      = strengthsEn;
        StrengthsUk      = strengthsUk;
        GapsEn           = gapsEn;
        GapsUk           = gapsUk;
        RecommendationEn = recommendationEn;
        RecommendationUk = recommendationUk;
        UpdatedAt        = DateTime.UtcNow;
    }


    public bool HasJudge => JudgeScore.HasValue;


    public bool HasReason => StrengthsEn is not null;


    public bool HasMono => MonoResultJson is not null;


    public void WriteMono(string monoResultJson)
    {
        if (string.IsNullOrWhiteSpace(monoResultJson))
            throw new ArgumentException("MonoResultJson cannot be empty", nameof(monoResultJson));
        MonoResultJson = monoResultJson;
        UpdatedAt      = DateTime.UtcNow;
    }

    private static void ValidateKey(string cvHash, string scoringVersion)
    {
        if (string.IsNullOrWhiteSpace(cvHash))
            throw new ArgumentException("CvHash cannot be empty", nameof(cvHash));
        if (string.IsNullOrWhiteSpace(scoringVersion))
            throw new ArgumentException("ScoringVersion cannot be empty", nameof(scoringVersion));
    }
}
