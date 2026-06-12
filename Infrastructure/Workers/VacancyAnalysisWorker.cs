using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;


public class VacancyAnalysisWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<VacancyAnalysisWorker> _logger;


    private const int BatchSize = 10;
    private static readonly TimeSpan IdleDelay = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan InterCallDelay = TimeSpan.FromMilliseconds(500);

    public VacancyAnalysisWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<VacancyAnalysisWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("VacancyAnalysisWorker started (batch={Batch}, idle={Idle}s)",
            BatchSize, IdleDelay.TotalSeconds);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IJobVacancyRepository>();
                var extract = scope.ServiceProvider.GetRequiredService<IVacancyExtractionService>();
                var expander = scope.ServiceProvider.GetRequiredService<ISkillExpansionService>();

                var jobs = await repo.GetJobsWithoutAnalysisAsync(BatchSize, ct);
                if (!jobs.Any())
                {
                    await Task.Delay(IdleDelay, ct);
                    continue;
                }

                _logger.LogInformation(
                    "VacancyAnalysisWorker: analyzing {Count} vacancies",
                    jobs.Count);

                int success = 0, failed = 0;
                foreach (var job in jobs)
                {
                    if (ct.IsCancellationRequested) break;
                    if (string.IsNullOrWhiteSpace(job.Description))
                    {

                        failed++;
                        continue;
                    }

                    try
                    {
                        var raw = $"{job.Title}\n\n{job.Description}";
                        var result = await extract.ExtractAsync(raw, ct);
                        if (string.IsNullOrWhiteSpace(result.Json))
                        {
                            _logger.LogWarning(
                                "VacancyAnalysisWorker: empty analysis for {Id} - skipping",
                                job.Id);
                            failed++;
                            continue;
                        }

                        await repo.SaveVacancyAnalysisAsync(
                            job.Id, result.Json, result.ModelVersion, ct);


                        try
                        {
                            var (skills, roleHint) = ExtractSkillsAndRoleHint(result.Json);
                            if (skills.Count > 0)
                            {
                                var exp = await expander.ExpandAsync(
                                    skills, "domain", roleHint, ct);
                                job.SetVacancyMustHavesExpansion(
                                    exp.ExpansionJson, expander.Version);
                                await repo.UpdateAsync(job, ct);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            throw;
                        }
                        catch (Exception expEx)
                        {
                            _logger.LogWarning(expEx,
                                "VacancyAnalysisWorker: skill expansion failed for {Id} - continuing without expansion",
                                job.Id);
                        }

                        success++;
                    }
                    catch (OperationCanceledException)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "VacancyAnalysisWorker: extract failed for {Id} ({Title})",
                            job.Id, job.Title);
                        failed++;
                    }

                    await Task.Delay(InterCallDelay, ct);
                }

                _logger.LogInformation(
                    "VacancyAnalysisWorker: batch done - success={Success}, failed={Failed}",
                    success, failed);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "VacancyAnalysisWorker: unhandled error, backing off");
                await Task.Delay(ErrorDelay, ct);
            }
        }

        _logger.LogInformation("VacancyAnalysisWorker stopped");
    }


    private static (List<string> Skills, string? RoleHint) ExtractSkillsAndRoleHint(string analysisJson)
    {
        var skills = new List<string>();
        string? roleHint = null;
        try
        {
            using var doc = JsonDocument.Parse(analysisJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (skills, null);

            AddStrings(root, "must_have_skills", skills);
            AddStrings(root, "nice_to_have_skills", skills);

            if (root.TryGetProperty("role_title", out var rt)
                && rt.ValueKind == JsonValueKind.Object
                && rt.TryGetProperty("en", out var en)
                && en.ValueKind == JsonValueKind.String)
            {
                var s = en.GetString();
                if (!string.IsNullOrWhiteSpace(s)) roleHint = s;
            }
        }
        catch (JsonException)
        {

        }
        return (skills, roleHint);
    }

    private static void AddStrings(JsonElement obj, string field, List<string> sink)
    {
        if (!obj.TryGetProperty(field, out var arr)) return;
        if (arr.ValueKind != JsonValueKind.Array) return;
        foreach (var item in arr.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString()?.Trim();
                if (!string.IsNullOrEmpty(s)) sink.Add(s);
            }
        }
    }
}
