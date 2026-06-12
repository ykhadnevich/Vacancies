using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;


public class JobEmbeddingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobEmbeddingWorker> _logger;

    private const int BatchSize = 50;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);

    public JobEmbeddingWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<JobEmbeddingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("JobEmbeddingWorker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IJobVacancyRepository>();
                var embedService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();

                var jobs = await repo.GetJobsWithoutEmbeddingAsync(BatchSize, ct);

                if (!jobs.Any())
                {
                    await Task.Delay(IdleDelay, ct);
                    continue;
                }

                _logger.LogInformation(
                    "JobEmbeddingWorker: embedding {Count} vacancies", jobs.Count);

                var texts = jobs
                    .Select(j => $"{j.Title}. {j.Description}")
                    .ToList();


                var mlEmbedService = (embedService as MlApi.MlApiEmbeddingService)!;
                var embeddings = await mlEmbedService.GetVacancyEmbeddingsBatchAsync(texts, ct);

                for (var i = 0; i < jobs.Count; i++)
                    jobs[i].SetEmbedding(embeddings[i]);

                await repo.SaveEmbeddingsAsync(jobs, ct);

                _logger.LogInformation(
                    "JobEmbeddingWorker: saved embeddings for {Count} vacancies", jobs.Count);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (System.Net.Http.HttpRequestException ex)
                when (ex.InnerException is System.Net.Sockets.SocketException)
            {


                _logger.LogWarning(
                    "JobEmbeddingWorker: ML API unavailable (localhost:8000). " +
                    "Embeddings skipped. Retrying in {Delay}s. Start the ML service to enable bi-encoder.",
                    ErrorDelay.TotalSeconds * 10);
                await Task.Delay(TimeSpan.FromSeconds(ErrorDelay.TotalSeconds * 10), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "JobEmbeddingWorker error, retrying in {Delay}s",
                    ErrorDelay.TotalSeconds);
                await Task.Delay(ErrorDelay, ct);
            }
        }

        _logger.LogInformation("JobEmbeddingWorker stopped");
    }
}
