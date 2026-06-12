using Domain.Scoring;

namespace Domain.Tests.Scoring;


public class ScoringConstantsTests
{
    [Fact]
    public void LinearWeights_Sum_To_One()
    {
        var sum =
            ScoringConstants.LinearWeights.Skill +
            ScoringConstants.LinearWeights.Seniority +
            ScoringConstants.LinearWeights.Experience +
            ScoringConstants.LinearWeights.RoleIntent +
            ScoringConstants.LinearWeights.Domain +
            ScoringConstants.LinearWeights.Language +
            ScoringConstants.LinearWeights.Education;

        Assert.Equal(1.0, sum, precision: 6);
    }


    [Fact]
    public void AntiFlag_Penalty_Is_Monotonically_NonIncreasing()
    {

        Assert.True(ScoringConstants.AntiFlag.PenaltyNone >= ScoringConstants.AntiFlag.PenaltyOne);
        Assert.True(ScoringConstants.AntiFlag.PenaltyOne  >= ScoringConstants.AntiFlag.PenaltyMany);
    }


    [Fact]
    public void AntiFlag_PenaltyNone_Is_One()
    {

        Assert.Equal(1.0, ScoringConstants.AntiFlag.PenaltyNone);
    }


    [Theory]
    [InlineData(0.0, 1.0)]
    public void DomainFloors_All_Within_Range(double min, double max)
    {
        Assert.InRange(ScoringConstants.DomainFloors.EmptyTech, min, max);
        Assert.InRange(ScoringConstants.DomainFloors.EmptyDomainHeavy, min, max);
        Assert.InRange(ScoringConstants.DomainFloors.EmptyDefault, min, max);
        Assert.InRange(ScoringConstants.DomainFloors.MatchTech, min, max);
        Assert.InRange(ScoringConstants.DomainFloors.MatchDomainHeavy, min, max);
        Assert.InRange(ScoringConstants.DomainFloors.MatchDefault, min, max);
    }


    [Fact]
    public void DomainFloors_TechIsTighterThan_DomainHeavy_And_Default()
    {

        Assert.True(ScoringConstants.DomainFloors.EmptyTech         <= ScoringConstants.DomainFloors.EmptyDomainHeavy);
        Assert.True(ScoringConstants.DomainFloors.EmptyDomainHeavy  <= ScoringConstants.DomainFloors.EmptyDefault);
        Assert.True(ScoringConstants.DomainFloors.MatchTech         <= ScoringConstants.DomainFloors.MatchDomainHeavy);
        Assert.True(ScoringConstants.DomainFloors.MatchDomainHeavy  <= ScoringConstants.DomainFloors.MatchDefault);
    }


    [Fact]
    public void RoleIntent_FallbackWeight_Is_Between_Zero_And_One()
    {
        Assert.InRange(ScoringConstants.RoleIntent.FallbackWeight, 0.0, 1.0);
    }


    [Fact]
    public void RoleIntent_Jaccard_Buckets_Are_Ordered()
    {

        Assert.True(ScoringConstants.RoleIntent.JaccardHigh > ScoringConstants.RoleIntent.JaccardMid);
        Assert.True(ScoringConstants.RoleIntent.JaccardMid > 0);
    }


    [Fact]
    public void RoleIntent_Scores_Are_Ordered_By_Bucket()
    {

        Assert.True(ScoringConstants.RoleIntent.ScoreHigh >= ScoringConstants.RoleIntent.ScoreMid);
        Assert.True(ScoringConstants.RoleIntent.ScoreMid  >= ScoringConstants.RoleIntent.ScoreLow);
        Assert.True(ScoringConstants.RoleIntent.ScoreLow  >= ScoringConstants.RoleIntent.ScoreNone);
    }


    [Fact]
    public void SkillMatch_Bonus_And_Threshold_Are_Reasonable()
    {

        Assert.InRange(ScoringConstants.SkillMatch.NiceToHaveBonus, 0.0, 1.0);
        Assert.InRange(ScoringConstants.SkillMatch.ExpansionThreshold, 0.0, 1.0);
    }


    [Fact]
    public void ExtremeBand_Low_Less_Than_High()
    {
        Assert.True(ScoringConstants.ExtremeBand.Low < ScoringConstants.ExtremeBand.High);
    }


    [Fact]
    public void CalibrationVersion_Is_Set()
    {

        Assert.False(string.IsNullOrWhiteSpace(ScoringConstants.CalibrationVersion));
    }
}
