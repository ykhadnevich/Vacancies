using System.Security.Cryptography;
using System.Text;

namespace Application.Jobs.Queries.GetAggregatedJobsV6;

/// <summary>
/// Computes a deterministic SHA-256 hash over a <see cref="GetAggregatedJobsV6Query"/>
/// so that the same logical search (same keywords + filters + limit, irrespective of
/// whitespace/case in keywords) maps to the same <c>UserSearchSnapshot</c> row.
/// </summary>
public static class V6QueryHasher
{
    public static string Compute(GetAggregatedJobsV6Query q)
    {
        var sb = new StringBuilder(256);
        sb.Append("v=1|");
        sb.Append("kw=").Append(Normalize(q.Keywords)).Append('|');
        sb.Append("loc=").Append(Normalize(q.Location)).Append('|');
        sb.Append("wf=").Append(q.WorkFormat?.ToString() ?? "_").Append('|');
        sb.Append("sl=").Append(q.SeniorityLevel?.ToString() ?? "_").Append('|');
        sb.Append("ms=").Append(q.MinSalary?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "_").Append('|');
        sb.Append("cat=").Append(Normalize(q.Category)).Append('|');
        sb.Append("lim=").Append(q.Limit);

        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()), hash);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "_";
        return value.Trim().ToLowerInvariant();
    }
}
