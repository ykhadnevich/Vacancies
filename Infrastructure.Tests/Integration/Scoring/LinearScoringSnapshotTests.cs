using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Scoring;
using Infrastructure.RelevancePipeline.V2.Scoring;
using Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;

namespace Infrastructure.Tests.Integration.Scoring;


/// <summary>
/// Regression-snapshot tests that exercise the full deterministic Linear scoring
/// pipeline (7 sub-axis calculators + AntiFlag + weighted composite) against
/// curated CV/vacancy pairs from <c>gold_set/</c> and <c>gold_set_v2/</c>.
///
/// Each pair has a Claude-Sonnet-rated <c>match_quality</c> from the v2 golden set
/// on the 0/2/4/6/8/10 anchor-only scale. We assert that Linear scores fall in
/// rating-appropriate bands and that ordinal relationships hold for the same CV
/// across pairs of different quality.
///
/// This is a deliberately loose snapshot — exact values would be brittle under
/// Day 3+ refactors (ESCO / embeddings). Bands + ordinals catch real regressions
/// without forcing rebaseline on every numeric tweak.
/// </summary>
public class LinearScoringSnapshotTests
{

    private static readonly Dictionary<SubScoreAxis, ISubScoreCalculator> Calculators =
        new List<ISubScoreCalculator>
        {
            new SkillMatchCalculator(),
            new SeniorityMatchCalculator(),
            new ExperienceMatchCalculator(),
            new LanguageMatchCalculator(),
            new EducationMatchCalculator(),
            new RoleIntentMatchCalculator(),
            new DomainAlignmentCalculator(),
        }.ToDictionary(c => c.Axis);


    private static readonly Dictionary<SubScoreAxis, double> Weights = new()
    {
        [SubScoreAxis.SkillMatch]       = ScoringConstants.LinearWeights.Skill,
        [SubScoreAxis.SeniorityMatch]   = ScoringConstants.LinearWeights.Seniority,
        [SubScoreAxis.ExperienceMatch]  = ScoringConstants.LinearWeights.Experience,
        [SubScoreAxis.LanguageMatch]    = ScoringConstants.LinearWeights.Language,
        [SubScoreAxis.EducationMatch]   = ScoringConstants.LinearWeights.Education,
        [SubScoreAxis.RoleIntentMatch]  = ScoringConstants.LinearWeights.RoleIntent,
        [SubScoreAxis.DomainAlignment]  = ScoringConstants.LinearWeights.Domain,
    };


    private static string RepoRoot
    {
        get
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (dir.GetDirectories("gold_set").Length > 0
                    && dir.GetDirectories("gold_set_v2").Length > 0)
                    return dir.FullName;
                dir = dir.Parent;
            }
            throw new InvalidOperationException(
                "Could not locate gold_set/gold_set_v2 directories above test binary.");
        }
    }


    private static (double score, double penalty) ComputeLinearScore(string cvName, string vacancyId)
    {
        var cvPath = Path.Combine(RepoRoot, "gold_set", "expected", cvName);
        var vacPath = Path.Combine(RepoRoot, "gold_set_v2", "vacancies", "expected", vacancyId + ".json");

        var cv = JsonDocument.Parse(File.ReadAllText(cvPath)).RootElement;
        var vacancy = JsonDocument.Parse(File.ReadAllText(vacPath)).RootElement;

        var weightedSum = Weights.Sum(kv =>
            Math.Clamp(Calculators[kv.Key].Compute(cv, vacancy), 0.0, 1.0) * kv.Value);

        var anti = AntiFlagEvaluator.Evaluate(cv, vacancy);
        var linear = Math.Clamp(weightedSum * anti.Penalty, 0.0, 1.0);

        return (linear, anti.Penalty);
    }


    [Theory]

    [InlineData("1_senior_backend_engineer.json",  "fd125a93-ce97-4995-abce-547c10bbdead", 10, 0.55)]

    [InlineData("13_ml_senior_engineer.json",      "166ff5b4-0ca5-40e8-ba41-074f2b5d9aa8", 10, 0.55)]

    [InlineData("14_frontend_mid_react.json",      "4a57e1ab-c2d6-46d5-8c16-94a5936b0dd4", 10, 0.55)]

    [InlineData("19_mobile_ios_mid.json",          "4282937a-c8f7-4849-b638-23a38a6125be", 10, 0.55)]
    public void HighQualityPairs_Score_Above_Mid_Band(
        string cvFile, string vacancyId, int rating, double minScore)
    {
        var (score, _) = ComputeLinearScore(cvFile, vacancyId);

        Assert.True(
            score >= minScore,
            $"Expected rating-{rating} pair {cvFile} × {vacancyId[..8]} to score >= {minScore}, got {score:F3}");
        Assert.InRange(score, 0.0, 1.0);
    }


    [Theory]


    [InlineData("1_senior_backend_engineer.json",  "ed7d41d9-7339-470b-8a23-ede9ab8a6da9", 0, 0.70)]

    [InlineData("13_ml_senior_engineer.json",      "ec898e00-26fa-4cca-89e4-3c3dcb8f47e8", 0, 0.70)]

    [InlineData("14_frontend_mid_react.json",      "459739e5-a61c-46ab-b761-282c7ede3e80", 0, 0.70)]
    public void LowQualityPairs_Score_Below_Catastrophic_Band(
        string cvFile, string vacancyId, int rating, double maxScore)
    {
        var (score, _) = ComputeLinearScore(cvFile, vacancyId);

        Assert.True(
            score <= maxScore,
            $"Expected rating-{rating} pair {cvFile} × {vacancyId[..8]} to score <= {maxScore}, got {score:F3}");
    }


    /// <summary>
    /// Documented limitation, kept as an executable regression record.
    ///
    /// Frontend-mid CV vs "MASTER Software Developer" (Microsoft SQL Server + WebSockets +
    /// MASTER ERP + database design + REST API, government domain) — rated 0 by Claude
    /// Sonnet because the role is a backend ERP position, completely cross-discipline.
    ///
    /// Linear still scores it ~0.55–0.65 because:
    ///   * Surface-form skill overlap on "REST API" and "WebSockets" gives ~40% of must-haves.
    ///   * Family-blind RoleIntent matches the token "developer".
    ///   * Empty/cross-domain alignment is not strongly penalised when the candidate is tech.
    ///
    /// We assert a generous upper band so this test passes today, but the gap between
    /// the rating (0) and the score (>0.5) is itself the regression we want documented.
    /// Day 3 (ESCO + embeddings) and Day 4 (family-specific weights) should narrow it;
    /// when they do, tighten <c>upperBand</c> accordingly.
    /// </summary>
    [Fact]
    public void Cross_Discipline_FrontendCv_vs_BackendErpVacancy_Is_Overscored()
    {
        const string cv = "14_frontend_mid_react.json";
        const string vacancyId = "459739e5-a61c-46ab-b761-282c7ede3e80";

        var (score, _) = ComputeLinearScore(cv, vacancyId);


        Assert.InRange(score, 0.45, 0.75);
    }


    [Theory]

    [InlineData("1_senior_backend_engineer.json",
        "fd125a93-ce97-4995-abce-547c10bbdead",
        "ed7d41d9-7339-470b-8a23-ede9ab8a6da9")]

    [InlineData("13_ml_senior_engineer.json",
        "166ff5b4-0ca5-40e8-ba41-074f2b5d9aa8",
        "ec898e00-26fa-4cca-89e4-3c3dcb8f47e8")]

    [InlineData("14_frontend_mid_react.json",
        "4a57e1ab-c2d6-46d5-8c16-94a5936b0dd4",
        "459739e5-a61c-46ab-b761-282c7ede3e80")]
    public void HighRatedPair_Outscores_LowRatedPair_ForSameCv(
        string cvFile, string highRatedVacancyId, string lowRatedVacancyId)
    {
        var (highScore, _) = ComputeLinearScore(cvFile, highRatedVacancyId);
        var (lowScore, _)  = ComputeLinearScore(cvFile, lowRatedVacancyId);

        Assert.True(
            highScore > lowScore,
            $"Expected high-rated pair to score above low-rated for {cvFile}: " +
            $"high={highScore:F3} vs low={lowScore:F3}");
    }


    [Theory]
    [InlineData("1_senior_backend_engineer.json", "fd125a93-ce97-4995-abce-547c10bbdead")]
    [InlineData("13_ml_senior_engineer.json",     "166ff5b4-0ca5-40e8-ba41-074f2b5d9aa8")]
    public void Clean_HighQuality_Pairs_Have_No_AntiFlag_Penalty(string cvFile, string vacancyId)
    {

        var (_, penalty) = ComputeLinearScore(cvFile, vacancyId);
        Assert.Equal(ScoringConstants.AntiFlag.PenaltyNone, penalty);
    }


    [Theory]
    [InlineData("1_senior_backend_engineer.json", "fd125a93-ce97-4995-abce-547c10bbdead")]
    [InlineData("13_ml_senior_engineer.json",     "166ff5b4-0ca5-40e8-ba41-074f2b5d9aa8")]
    [InlineData("14_frontend_mid_react.json",     "4a57e1ab-c2d6-46d5-8c16-94a5936b0dd4")]
    public void All_SubScores_Stay_Within_Unit_Interval(string cvFile, string vacancyId)
    {

        var cvPath = Path.Combine(RepoRoot, "gold_set", "expected", cvFile);
        var vacPath = Path.Combine(RepoRoot, "gold_set_v2", "vacancies", "expected", vacancyId + ".json");

        var cv = JsonDocument.Parse(File.ReadAllText(cvPath)).RootElement;
        var vacancy = JsonDocument.Parse(File.ReadAllText(vacPath)).RootElement;

        foreach (var (axis, calc) in Calculators)
        {
            var raw = calc.Compute(cv, vacancy);
            Assert.InRange(raw, 0.0, 1.0);
        }
    }
}
