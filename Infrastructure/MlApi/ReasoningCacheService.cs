using Application.Common.Interfaces;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Infrastructure.MlApi;


public class ReasoningCacheService : IReasoningCacheService
{
    private readonly AppDbContext _context;
    private readonly ILogger<ReasoningCacheService> _logger;

    public ReasoningCacheService(AppDbContext context, ILogger<ReasoningCacheService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<CachedReason?> GetReasonAsync(
        Guid cvVersionId,
        Guid jobId,
        CancellationToken ct = default,
        string? requiredModelVersionPrefix = null)
    {
        var cached = await _context.RelevanceExplanations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                e => e.CvVersionId == cvVersionId && e.JobId == jobId, ct);

        if (cached is null) return null;


        if (requiredModelVersionPrefix is not null
            && !cached.ModelVersion.StartsWith(requiredModelVersionPrefix, StringComparison.Ordinal))
        {
            _logger.LogDebug(
                "Cache miss (stale prompt): job={JobId}, cached={Cached}, required prefix={Required}",
                jobId, cached.ModelVersion, requiredModelVersionPrefix);
            return null;
        }


        var score = cached.Score > 0 ? (float?)cached.Score : null;
        return new CachedReason(cached.Reason, score);
    }

    public async Task SaveReasonAsync(
        Guid cvVersionId,
        Guid jobId,
        string reason,
        float score,
        string modelVersion,
        CancellationToken ct = default)
    {
        var existing = await _context.RelevanceExplanations
            .FirstOrDefaultAsync(
                e => e.CvVersionId == cvVersionId && e.JobId == jobId, ct);

        if (existing is not null)
        {
            existing.Reason = reason;
            existing.Score = score;
            existing.ModelVersion = modelVersion;
            existing.GeneratedAt = DateTime.UtcNow;
        }
        else
        {
            _context.RelevanceExplanations.Add(new RelevanceExplanation
            {
                CvVersionId = cvVersionId,
                JobId = jobId,
                Reason = reason,
                Score = score,
                ModelVersion = modelVersion,
                GeneratedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync(ct);

        _logger.LogDebug(
            "Reason cached for (cv={CvVersion}, job={JobId})", cvVersionId, jobId);
    }

    public async Task<IReadOnlyList<PendingReasoningItem>> GetPendingItemsAsync(
        Guid cvVersionId,
        int limit,
        CancellationToken ct = default)
    {


        var cachedJobIds = await _context.RelevanceExplanations
            .AsNoTracking()
            .Where(e => e.CvVersionId == cvVersionId)
            .Select(e => e.JobId)
            .ToListAsync(ct);

        var userProfile = await _context.UserProfiles
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.CvVersionId == cvVersionId, ct);

        if (userProfile is null) return [];

        var pending = await _context.JobVacancies
            .AsNoTracking()
            .Where(j =>
                !cachedJobIds.Contains(j.Id) &&
                j.RelevanceScore != null &&
                !string.IsNullOrEmpty(j.Description))
            .OrderByDescending(j => j.RelevanceScore!.Value)
            .Take(limit)
            .ToListAsync(ct);


        var cvText = !string.IsNullOrWhiteSpace(userProfile.CvSummary)
            ? userProfile.CvSummary
            : (userProfile.CvRawText ?? string.Empty)[..Math.Min(3500, (userProfile.CvRawText ?? string.Empty).Length)];


        if (string.IsNullOrWhiteSpace(cvText)) return [];

        return pending
            .Select(j => new PendingReasoningItem(
                CvVersionId: cvVersionId,
                JobId: j.Id,
                CvText: cvText,
                JobTitle: j.Title,
                JobDesc: j.Description ?? string.Empty,
                Score: j.RelevanceScore!.Value))
            .ToList();
    }
}
