using Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;

public sealed class CacheRetentionWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CacheRetentionWorker> _logger;
    private readonly RetentionOptions _options;

    private static readonly TimeSpan InitialDelay  = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(24);
    private static readonly TimeSpan ErrorBackoff  = TimeSpan.FromMinutes(30);

    public CacheRetentionWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<CacheRetentionWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = configuration.GetSection("CacheRetention").Get<RetentionOptions>()
                   ?? new RetentionOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "CacheRetentionWorker started — snapshots={Snap}d, scoring={Score}d, cost={Cost}d, audit={Audit}d",
            _options.UserSearchSnapshotDays, _options.ScoringCacheDays,
            _options.GeminiCostLogDays, _options.AuditEntryDays);

        try { await Task.Delay(InitialDelay, ct); }
        catch (OperationCanceledException) { return; }

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await SweepAsync(ct);
                await Task.Delay(SweepInterval, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "CacheRetentionWorker: sweep failed — backing off {Mins} min", ErrorBackoff.TotalMinutes);
                try { await Task.Delay(ErrorBackoff, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("CacheRetentionWorker stopped");
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var now = DateTime.UtcNow;
        int total = 0;

        if (_options.UserSearchSnapshotDays > 0)
        {
            var cutoff = now - TimeSpan.FromDays(_options.UserSearchSnapshotDays);
            var deleted = await db.UserSearchSnapshots
                .Where(s => s.ExecutedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            total += deleted;
            _logger.LogInformation(
                "CacheRetention: UserSearchSnapshots — deleted {Count} rows older than {Cutoff:o}",
                deleted, cutoff);
        }

        if (_options.ScoringCacheDays > 0)
        {
            var cutoff = now - TimeSpan.FromDays(_options.ScoringCacheDays);
            var deleted = await db.ScoringCache
                .Where(s => s.CreatedAt < cutoff)
                .ExecuteDeleteAsync(ct);
            total += deleted;
            _logger.LogInformation(
                "CacheRetention: ScoringCache — deleted {Count} rows older than {Cutoff:o}",
                deleted, cutoff);
        }

        if (_options.GeminiCostLogDays > 0)
        {
            var cutoff = now - TimeSpan.FromDays(_options.GeminiCostLogDays);
            var deleted = await db.GeminiCostLog
                .Where(c => c.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);
            total += deleted;
            _logger.LogInformation(
                "CacheRetention: GeminiCostLog — deleted {Count} rows older than {Cutoff:o}",
                deleted, cutoff);
        }

        if (_options.AuditEntryDays > 0)
        {
            var cutoff = now - TimeSpan.FromDays(_options.AuditEntryDays);
            var deleted = await db.AuditEntries
                .Where(a => a.Timestamp < cutoff)
                .ExecuteDeleteAsync(ct);
            total += deleted;
            _logger.LogInformation(
                "CacheRetention: AuditEntries — deleted {Count} rows older than {Cutoff:o}",
                deleted, cutoff);
        }

        _logger.LogInformation("CacheRetention: sweep complete — {Total} rows deleted total", total);
    }
}

public sealed class RetentionOptions
{
    public int UserSearchSnapshotDays { get; set; } = 30;
    public int ScoringCacheDays       { get; set; } = 90;
    public int GeminiCostLogDays      { get; set; } = 180;
    public int AuditEntryDays         { get; set; } = 365;
}
