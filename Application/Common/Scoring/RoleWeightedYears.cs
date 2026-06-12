using System.Collections;

namespace Application.Common.Scoring;


public sealed class RoleWeightedYears
    : IReadOnlyDictionary<RoleBucketId, double>, IEquatable<RoleWeightedYears>
{
    private readonly Dictionary<RoleBucketId, double> _buckets;


    public RoleWeightedYears(
        double PmPo,
        double Pmm,
        double BusinessAnalyst,
        double ProjectManager,
        double Developer,
        double DataAnalyst,
        double Designer,
        double Marketing)
    {
        _buckets = new Dictionary<RoleBucketId, double>
        {
            [RoleBucketId.PmPo]            = PmPo,
            [RoleBucketId.Pmm]             = Pmm,
            [RoleBucketId.BusinessAnalyst] = BusinessAnalyst,
            [RoleBucketId.ProjectManager]  = ProjectManager,
            [RoleBucketId.Developer]       = Developer,
            [RoleBucketId.DataAnalyst]     = DataAnalyst,
            [RoleBucketId.Designer]        = Designer,
            [RoleBucketId.Marketing]       = Marketing,
        };
    }


    public RoleWeightedYears(IReadOnlyDictionary<RoleBucketId, double> buckets)
    {
        _buckets = new Dictionary<RoleBucketId, double>(buckets);
    }


    public double PmPo            => _buckets.GetValueOrDefault(RoleBucketId.PmPo);
    public double Pmm             => _buckets.GetValueOrDefault(RoleBucketId.Pmm);
    public double BusinessAnalyst => _buckets.GetValueOrDefault(RoleBucketId.BusinessAnalyst);
    public double ProjectManager  => _buckets.GetValueOrDefault(RoleBucketId.ProjectManager);
    public double Developer       => _buckets.GetValueOrDefault(RoleBucketId.Developer);
    public double DataAnalyst     => _buckets.GetValueOrDefault(RoleBucketId.DataAnalyst);
    public double Designer        => _buckets.GetValueOrDefault(RoleBucketId.Designer);
    public double Marketing       => _buckets.GetValueOrDefault(RoleBucketId.Marketing);


    public double Get(RoleBucketId id) => _buckets.GetValueOrDefault(id);


    public double this[RoleBucketId key] => _buckets[key];
    public IEnumerable<RoleBucketId> Keys => _buckets.Keys;
    public IEnumerable<double> Values => _buckets.Values;
    public int Count => _buckets.Count;
    public bool ContainsKey(RoleBucketId key) => _buckets.ContainsKey(key);
    public bool TryGetValue(RoleBucketId key, out double value) => _buckets.TryGetValue(key, out value);
    public IEnumerator<KeyValuePair<RoleBucketId, double>> GetEnumerator() => _buckets.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();


    public bool Equals(RoleWeightedYears? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        if (_buckets.Count != other._buckets.Count) return false;

        foreach (var (key, value) in _buckets)
        {
            if (!other._buckets.TryGetValue(key, out var otherValue))
                return false;
            if (Math.Abs(value - otherValue) > 1e-9)
                return false;
        }
        return true;
    }

    public override bool Equals(object? obj) => obj is RoleWeightedYears rwy && Equals(rwy);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        foreach (var kv in _buckets.OrderBy(p => p.Key.Id, StringComparer.Ordinal))
        {
            hash.Add(kv.Key);
            hash.Add(Math.Round(kv.Value, 6));
        }
        return hash.ToHashCode();
    }

    public static bool operator ==(RoleWeightedYears? left, RoleWeightedYears? right)
        => left is null ? right is null : left.Equals(right);

    public static bool operator !=(RoleWeightedYears? left, RoleWeightedYears? right)
        => !(left == right);

    public override string ToString()
    {
        var parts = _buckets
            .OrderBy(kv => kv.Key.Id, StringComparer.Ordinal)
            .Select(kv => $"{kv.Key.Id}={kv.Value:F1}");
        return $"RoleWeightedYears({string.Join(", ", parts)})";
    }
}
