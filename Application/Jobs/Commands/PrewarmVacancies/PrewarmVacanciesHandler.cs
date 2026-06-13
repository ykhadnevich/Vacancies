using System.Diagnostics;
using System.Text.Json;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Application.Jobs.Commands.PrewarmVacancies;

public sealed class PrewarmVacanciesHandler
    : IRequestHandler<PrewarmVacanciesCommand, PrewarmVacanciesResult>
{
    private readonly IJobAggregationService _aggregator;
    private readonly IVacancyExtractionService _extractor;
    private readonly ISkillExpansionService _expander;
    private readonly IJobVacancyRepository _repo;
    private readonly ILogger<PrewarmVacanciesHandler> _logger;

    public PrewarmVacanciesHandler(
        IJobAggregationService aggregator,
        IVacancyExtractionService extractor,
        ISkillExpansionService expander,
        IJobVacancyRepository repo,
        ILogger<PrewarmVacanciesHandler> logger)
    {
        _aggregator = aggregator;
        _extractor = extractor;
        _expander = expander;
        _repo = repo;
        _logger = logger;
    }

    public async Task<PrewarmVacanciesResult> Handle(
        PrewarmVacanciesCommand cmd, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        _logger.LogInformation(
            "PrewarmVacancies: starting keywords='{Keywords}' country={Country} max={Max}",
            cmd.Keywords, cmd.Country, cmd.MaxNewVacancies);

        var agg = await _aggregator.ScrapeAndPersistAsync(
            cmd.Keywords, cmd.Location, cmd.Country, ct);

        var toNormalize = agg.NewlyInserted
            .Where(j => !string.IsNullOrWhiteSpace(j.Description))
            .Take(Math.Max(1, cmd.MaxNewVacancies))
            .ToList();

        int normalized = 0, normFailed = 0, expandFailed = 0;

        foreach (var job in toNormalize)
        {
            if (ct.IsCancellationRequested) break;

            try
            {
                var raw = $"{job.Title}\n\n{job.Description}";
                var result = await _extractor.ExtractAsync(raw, ct);

                if (string.IsNullOrWhiteSpace(result.Json))
                {
                    normFailed++;
                    _logger.LogWarning(
                        "PrewarmVacancies: empty extraction result for {Id} '{Title}'",
                        job.Id, job.Title);
                    continue;
                }

                await _repo.SaveVacancyAnalysisAsync(
                    job.Id, result.Json, result.ModelVersion, ct);

                try
                {
                    var (skills, roleHint) = ExtractSkillsAndRoleHint(result.Json);
                    if (skills.Count > 0)
                    {
                        var exp = await _expander.ExpandAsync(
                            skills, "domain", roleHint, ct);
                        job.SetVacancyMustHavesExpansion(
                            exp.ExpansionJson, _expander.Version);
                        await _repo.UpdateAsync(job, ct);
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception expEx)
                {
                    expandFailed++;
                    _logger.LogDebug(expEx,
                        "PrewarmVacancies: skill expansion failed for {Id} — continuing without",
                        job.Id);
                }

                normalized++;
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                normFailed++;
                _logger.LogWarning(ex,
                    "PrewarmVacancies: normalize failed for {Id} '{Title}'",
                    job.Id, job.Title);
            }
        }

        sw.Stop();

        _logger.LogInformation(
            "PrewarmVacancies: done in {Ms}ms — scraped={Scraped}, dups={Dups}, " +
            "newlyInserted={New}, normalized={Norm}, normFailed={NormFail}, expandFailed={ExpFail}",
            sw.ElapsedMilliseconds, agg.ScrapedTotal, agg.DuplicatesRemoved,
            agg.NewlyInserted.Count, normalized, normFailed, expandFailed);

        return new PrewarmVacanciesResult(
            Scraped:              agg.ScrapedTotal,
            DuplicatesRemoved:    agg.DuplicatesRemoved,
            NewlyInserted:        agg.NewlyInserted.Count,
            Normalized:           normalized,
            NormalizationFailed:  normFailed,
            SkillExpansionFailed: expandFailed,
            DurationMs:           sw.ElapsedMilliseconds);
    }

    private static (List<string> Skills, string? RoleHint) ExtractSkillsAndRoleHint(
        string analysisJson)
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
        catch (JsonException) { }

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
