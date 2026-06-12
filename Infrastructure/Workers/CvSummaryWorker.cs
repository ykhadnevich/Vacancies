using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Workers;


public class CvSummaryWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CvSummaryWorker> _logger;

    private static readonly TimeSpan IdleDelay  = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ErrorDelay = TimeSpan.FromSeconds(30);

    public CvSummaryWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<CvSummaryWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        _logger.LogInformation("CvSummaryWorker started");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var userRepo  = scope.ServiceProvider.GetRequiredService<IUserProfileRepository>();
                var extractor = scope.ServiceProvider.GetRequiredService<ICvExtractionService>();
                var promptBuilder = scope.ServiceProvider.GetRequiredService<ICvNormalizationPromptBuilder>();
                var expander  = scope.ServiceProvider.GetRequiredService<ISkillExpansionService>();


                var pending = await userRepo.GetUsersNeedingNormalizationAsync(
                    promptBuilder.CurrentExpectedModelVersionPrefix, ct);

                if (!pending.Any())
                {
                    await Task.Delay(IdleDelay, ct);
                    continue;
                }

                foreach (var user in pending)
                {
                    if (ct.IsCancellationRequested) break;

                    try
                    {
                        _logger.LogInformation(
                            "CvSummaryWorker: extracting summary for user {UserId}", user.Id);

                        var result = await extractor.ExtractAsync(user.CvRawText!, ct);

                        if (!string.IsNullOrWhiteSpace(result.Summary)
                            && result.ModelVersion != string.Empty)
                        {
                            user.SetCvSummary(result.Summary, result.ModelVersion);


                            try
                            {
                                var skills = ExtractCvSkills(result.Summary);
                                if (skills.Count > 0)
                                {
                                    var exp = await expander.ExpandAsync(
                                        skills, "domain", roleFamilyHint: null, ct);
                                    user.SetCvSkillsExpansion(
                                        exp.ExpansionJson, expander.Version);
                                }
                            }
                            catch (OperationCanceledException)
                            {
                                throw;
                            }
                            catch (Exception expEx)
                            {
                                _logger.LogWarning(expEx,
                                    "CvSummaryWorker: skill expansion failed for user {UserId} - continuing without expansion",
                                    user.Id);
                            }

                            await userRepo.UpdateAsync(user, ct);

                            _logger.LogInformation(
                                "CvSummaryWorker: saved summary for user {UserId} ({Chars} chars)",
                                user.Id, result.Summary.Length);
                        }
                        else
                        {
                            _logger.LogWarning(
                                "CvSummaryWorker: ML API returned empty summary for user {UserId}, will retry",
                                user.Id);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex,
                            "CvSummaryWorker: failed for user {UserId}", user.Id);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "CvSummaryWorker outer error, retrying in {Delay}s", ErrorDelay.TotalSeconds);
                await Task.Delay(ErrorDelay, ct);
            }
        }

        _logger.LogInformation("CvSummaryWorker stopped");
    }


    private static List<string> ExtractCvSkills(string cvSummaryJson)
    {
        var skills = new List<string>();
        try
        {
            using var doc = JsonDocument.Parse(cvSummaryJson);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object) return skills;
            AddStrings(root, "technical_skills", skills);
            AddStrings(root, "domain_skills", skills);
        }
        catch (JsonException)
        {

        }
        return skills;
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
