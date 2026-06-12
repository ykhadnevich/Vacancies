using Application.Common.Scoring;

namespace Application.Tests.Common.Scoring;


public class RoleWeightedYearsTests
{
    [Fact]
    public void PositionalCtor_ExposesAllEightBuckets_ViaPropertyAccessors()
    {
        var rwy = new RoleWeightedYears(
            PmPo: 1.5, Pmm: 2.0, BusinessAnalyst: 0.5, ProjectManager: 0.0,
            Developer: 3.0, DataAnalyst: 0.0, Designer: 0.0, Marketing: 0.0);

        Assert.Equal(1.5, rwy.PmPo);
        Assert.Equal(2.0, rwy.Pmm);
        Assert.Equal(0.5, rwy.BusinessAnalyst);
        Assert.Equal(0.0, rwy.ProjectManager);
        Assert.Equal(3.0, rwy.Developer);
        Assert.Equal(0.0, rwy.DataAnalyst);
        Assert.Equal(0.0, rwy.Designer);
        Assert.Equal(0.0, rwy.Marketing);
    }

    [Fact]
    public void Equals_ReturnsTrue_ForSameBucketValues()
    {
        var a = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);
        var b = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);

        Assert.True(a.Equals(b));
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void Equals_ReturnsFalse_ForDifferentBucketValues()
    {
        var a = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);
        var b = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.1, 0.0, 0.0, 0.0);

        Assert.False(a.Equals(b));
        Assert.False(a == b);
        Assert.True(a != b);
    }

    [Fact]
    public void Equals_HandlesNull_Correctly()
    {
        var a = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);

        Assert.False(a.Equals((RoleWeightedYears?)null));
        Assert.False(a.Equals((object?)null));
        Assert.False(a == null);
        Assert.True(a != null);
        Assert.True(((RoleWeightedYears?)null) == null);
    }

    [Fact]
    public void Equals_HandlesEpsilon_TreatsCloseValuesAsEqual()
    {
        var a = new RoleWeightedYears(1.5, 0, 0, 0, 0, 0, 0, 0);
        var b = new RoleWeightedYears(1.5 + 1e-10, 0, 0, 0, 0, 0, 0, 0);

        Assert.True(a.Equals(b), "Values differing by < 1e-9 must be considered equal.");
    }

    [Fact]
    public void DictAccess_ReturnsSameValue_AsPropertyAccessor()
    {
        var rwy = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);

        Assert.Equal(rwy.PmPo, rwy[RoleBucketId.PmPo]);
        Assert.Equal(rwy.Pmm, rwy[RoleBucketId.Pmm]);
        Assert.Equal(rwy.Developer, rwy[RoleBucketId.Developer]);
    }

    [Fact]
    public void DictCtor_AcceptsExtraBuckets_BeyondV1PmFamily()
    {


        var rwy = new RoleWeightedYears(new Dictionary<RoleBucketId, double>
        {
            [RoleBucketId.Backend]  = 3.2,
            [RoleBucketId.Frontend] = 1.5,
            [RoleBucketId.DevOps]   = 0.5,
        });

        Assert.Equal(3.2, rwy.Get(RoleBucketId.Backend));
        Assert.Equal(1.5, rwy.Get(RoleBucketId.Frontend));
        Assert.Equal(0.5, rwy.Get(RoleBucketId.DevOps));


        Assert.Equal(0.0, rwy.PmPo);
        Assert.Equal(0.0, rwy.Developer);
    }

    [Fact]
    public void Get_ReturnsZero_ForUnknownBucket()
    {
        var rwy = new RoleWeightedYears(1.5, 0, 0, 0, 0, 0, 0, 0);


        Assert.Equal(1.5, rwy.Get(RoleBucketId.PmPo));
        Assert.Equal(0.0, rwy.Get(RoleBucketId.Backend));
        Assert.Equal(0.0, rwy.Get(RoleBucketId.MlEngineer));
    }

    [Fact]
    public void Equals_DifferentBucketSets_AreNotEqual()
    {
        var pmFamily = new RoleWeightedYears(1.0, 0, 0, 0, 0, 0, 0, 0);
        var engFamily = new RoleWeightedYears(new Dictionary<RoleBucketId, double>
        {
            [RoleBucketId.Backend] = 1.0,
        });

        Assert.False(pmFamily.Equals(engFamily),
            "Different bucket schemas must not compare equal even if magnitudes look similar.");
    }

    [Fact]
    public void GetHashCode_StableAcrossInstances()
    {

        var a = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);
        var b = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);

        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(a.GetHashCode(), a.GetHashCode());
    }

    [Fact]
    public void IReadOnlyDictionary_EnumerableExposesAllBuckets()
    {
        var rwy = new RoleWeightedYears(1.5, 2.0, 0.5, 0.0, 3.0, 0.0, 0.0, 0.0);
        var asDict = (IReadOnlyDictionary<RoleBucketId, double>)rwy;

        Assert.Equal(8, asDict.Count);
        Assert.True(asDict.ContainsKey(RoleBucketId.PmPo));
        Assert.True(asDict.TryGetValue(RoleBucketId.Developer, out var dev));
        Assert.Equal(3.0, dev);
    }
}
