using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;

public sealed class DescriptionRetryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<DescriptionRetryWorker> _logger;
    private readonly DescriptionRetryOptions _options;

    private static readonly TimeSpan InitialDelay = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan ErrorBackoff = TimeSpan.FromMinutes(15);

    public DescriptionRetryWorker(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DescriptionRetryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _options = configuration.GetSection("DescriptionRetry").Get<DescriptionRetryOptions>()
                   ?? new DescriptionRetryOptions();
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation(
            "DescriptionRetryWorker started — interval={Interval}min, batch={Batch}, maxAge={Age}d",
            _options.RetryIntervalMinutes, _options.BatchSize, _options.MaxAgeDays);

        try { await Task.Delay(InitialDelay, ct); }
        catch (OperationCanceledException) { return; }

        var interval = TimeSpan.FromMinutes(Math.Max(5, _options.RetryIntervalMinutes));

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await RetryPassAsync(ct);
                await Task.Delay(interval, ct);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "DescriptionRetryWorker: pass failed — backing off {Mins} min", ErrorBackoff.TotalMinutes);
                try { await Task.Delay(ErrorBackoff, ct); }
                catch (OperationCanceledException) { break; }
            }
        }

        _logger.LogInformation("DescriptionRetryWorker stopped");
    }

    private async Task RetryPassAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo    = scope.ServiceProvider.GetRequiredService<IJobVacancyRepository>();
        var fetcher = scope.ServiceProvider.GetRequiredService<IJobDescriptionFetcher>();

        var jobs = await repo.GetJobsWithEmptyDescriptionAsync(
            batch:      _options.BatchSize,
            maxAgeDays: _options.MaxAgeDays,
            ct:         ct);

        if (jobs.Count == 0)
        {
            _logger.LogDebug("DescriptionRetry: no candidates in batch");
            return;
        }

        int success = 0, failed = 0, stillEmpty = 0;

        foreach (var job in jobs)
        {
            if (ct.IsCancellationRequested) break;
            if (string.IsNullOrWhiteSpace(job.PrimaryUrl)) continue;

            try
            {
                var html = await fetcher.FetchDescriptionAsync(job.PrimaryUrl, ct);
                if (string.IsNullOrWhiteSpace(html))
                {
                    stillEmpty++;
                    continue;
                }

                job.UpdateDescription(html);
                await repo.UpdateAsync(job, ct);
                success++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                failed++;
                _logger.LogDebug(ex,
                    "DescriptionRetry: failed to fetch {Url}: {Message}",
                    job.PrimaryUrl, ex.Message);
            }
        }

        _logger.LogInformation(
            "DescriptionRetry: pass complete — candidates={Total}, recovered={Success}, still-empty={Empty}, failed={Failed}",
            jobs.Count, success, stillEmpty, failed);
    }
}

public sealed class DescriptionRetryOptions
{
    public int RetryIntervalMinutes { get; set; } = 60;
    public int BatchSize { get; set; } = 25;
    public int MaxAgeDays { get; set; } = 7;
}
