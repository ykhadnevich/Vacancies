using Domain.Enums;
using Domain.Scoring;

namespace Domain.Tests.Scoring;


public class SeniorityBoundariesTests
{
    [Theory]
    [InlineData(0, SeniorityLevel.NotSpecified)]
    [InlineData(-1, SeniorityLevel.NotSpecified)]
    [InlineData(1, SeniorityLevel.Junior)]
    [InlineData(2, SeniorityLevel.Middle)]
    [InlineData(3, SeniorityLevel.Middle)]
    [InlineData(4, SeniorityLevel.Senior)]
    [InlineData(5, SeniorityLevel.Senior)]
    [InlineData(6, SeniorityLevel.Lead)]
    [InlineData(10, SeniorityLevel.Lead)]
    public void FromYears_returns_expected_level(int years, SeniorityLevel expected)
    {
        Assert.Equal(expected, SeniorityBoundaries.FromYears(years));
    }


    [Theory]
    [InlineData(SeniorityLevel.Internship, 0)]
    [InlineData(SeniorityLevel.Junior, 1)]
    [InlineData(SeniorityLevel.Middle, 3)]
    [InlineData(SeniorityLevel.Senior, 5)]
    [InlineData(SeniorityLevel.Lead, 6)]
    [InlineData(SeniorityLevel.NotSpecified, 0)]
    public void MinYears_returns_expected_floor(SeniorityLevel level, int expected)
    {
        Assert.Equal(expected, SeniorityBoundaries.MinYears(level));
    }


    [Theory]
    [InlineData("junior", SeniorityLevel.Junior)]
    [InlineData("jr", SeniorityLevel.Junior)]
    [InlineData("middle", SeniorityLevel.Middle)]
    [InlineData("mid", SeniorityLevel.Middle)]
    [InlineData("senior", SeniorityLevel.Senior)]
    [InlineData("sr", SeniorityLevel.Senior)]
    [InlineData("lead", SeniorityLevel.Lead)]
    [InlineData("principal", SeniorityLevel.Lead)]
    [InlineData("staff", SeniorityLevel.Lead)]
    [InlineData("head", SeniorityLevel.Lead)]
    [InlineData("chief", SeniorityLevel.Lead)]
    [InlineData("intern", SeniorityLevel.Internship)]
    [InlineData("trainee", SeniorityLevel.Internship)]
    [InlineData("  SENIOR  ", SeniorityLevel.Senior)]
    [InlineData("", SeniorityLevel.NotSpecified)]
    [InlineData("nonsense", SeniorityLevel.NotSpecified)]
    [InlineData(null, SeniorityLevel.NotSpecified)]
    public void FromString_maps_common_aliases(string? raw, SeniorityLevel expected)
    {
        Assert.Equal(expected, SeniorityBoundaries.FromString(raw));
    }


    [Theory]
    [InlineData(SeniorityLevel.Junior, "junior")]
    [InlineData(SeniorityLevel.Middle, "middle")]
    [InlineData(SeniorityLevel.Senior, "senior")]
    [InlineData(SeniorityLevel.Lead, "lead")]
    [InlineData(SeniorityLevel.Internship, "intern")]
    [InlineData(SeniorityLevel.NotSpecified, "not_specified")]
    public void ToCanonicalString_returns_canonical(SeniorityLevel level, string expected)
    {
        Assert.Equal(expected, SeniorityBoundaries.ToCanonicalString(level));
    }


    [Theory]
    [InlineData(SeniorityLevel.Junior)]
    [InlineData(SeniorityLevel.Middle)]
    [InlineData(SeniorityLevel.Senior)]
    [InlineData(SeniorityLevel.Lead)]
    public void Round_trip_FromYears_at_MinYears_is_stable(SeniorityLevel level)
    {
        int minYears = SeniorityBoundaries.MinYears(level);
        Assert.Equal(level, SeniorityBoundaries.FromYears(minYears));
    }


    [Fact]
    public void Senior_boundary_is_5_not_6()
    {

        Assert.Equal(SeniorityLevel.Senior, SeniorityBoundaries.FromYears(5));
        Assert.Equal(SeniorityLevel.Lead, SeniorityBoundaries.FromYears(6));
    }
}
