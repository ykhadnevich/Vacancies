using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;


public class ReasoningWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ReasoningWorker> _logger;

    private const int BatchSize = 20;
    private static readonly TimeSpan IdleDelay  = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);


    private static readonly TimeSpan GroqCallDelay = TimeSpan.FromSeconds(3);

    public ReasoningWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<ReasoningWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("ReasoningWorker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var userRepo = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
                var cache = scope.ServiceProvider.GetRequiredService<IReasoningCacheService>();
                var reasoning = scope.ServiceProvider.GetRequiredService<IReasoningService>();

                var users = await userRepo.GetUsersWithCvAsync(ct);

                var anyWork = false;

                foreach (var user in users)
                {
                    var pending = await cache.GetPendingItemsAsync(
                        user.CvVersionId, BatchSize, ct);

                    if (!pending.Any()) continue;

                    anyWork = true;
                    _logger.LogInformation(
                        "ReasoningWorker: generating {Count} reasons for user {UserId}",
                        pending.Count, user.Id);

                    foreach (var item in pending)
                    {
                        if (ct.IsCancellationRequested) break;

                        try
                        {
                            var result = await reasoning.GenerateReasonAsync(
                                item.CvText,
                                item.JobTitle,
                                item.JobDesc,
                                item.Score,
                                ct);


                            var isFallback = result.ModelVersion == "rule-based-v1"
                                || (result.ModelVersion?.StartsWith("summary-match") ?? false);

                            if (!string.IsNullOrEmpty(result.Reason) && !isFallback)
                            {
                                await cache.SaveReasonAsync(
                                    item.CvVersionId, item.JobId,
                                    result.Reason, item.Score, result.ModelVersion ?? string.Empty, ct);
                            }
                            else if (isFallback)
                            {
                                _logger.LogWarning(
                                    "ReasoningWorker: ML API returned offline fallback ({Version}) for [{JobTitle}], skipping cache — will retry",
                                    result.ModelVersion, item.JobTitle);
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex,
                                "ReasoningWorker: failed for job {JobId}", item.JobId);
                        }


                        await Task.Delay(GroqCallDelay, ct);
                    }
                }

                if (!anyWork)
                    await Task.Delay(IdleDelay, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReasoningWorker outer error, retrying in {Delay}s",
                    ErrorDelay.TotalSeconds);
                await Task.Delay(ErrorDelay, ct);
            }
        }

        _logger.LogInformation("ReasoningWorker stopped");
    }
}
