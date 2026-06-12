using System.Diagnostics;
using System.Text.Json;
using Application.Common.Interfaces;
using Microsoft.Extensions.Logging;

namespace EvalTool.Pipeline;

/// <summary>
/// Scores all CV×vacancy pairs from the held-out gold set
/// (gold_set_v2/match_quality_heldout/_aggregated/per_pair_resolved.json) using the
/// production <see cref="IRecruiterScoringService"/>. Writes a JSON file containing
/// per-pair predicted composite score + sub-scores + cost + latency + model version,
/// suitable for downstream consumption by:
///   - Step 3 metrics (Spearman/QWK/NDCG/ECE/reliability)
///   - LangSmith Experiment uploader (Python sidecar)
/// </summary>
public sealed class HeldoutScorer
{
    private readonly IRecruiterScoringService _scoring;
    private readonly ILogger<HeldoutScorer> _logger;
    private static readonly JsonSerializerOptions JsonReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private static readonly JsonSerializerOptions JsonWriteOpts = new()
    {
        WriteIndented = true,
        // SnakeCaseLower keeps the outer anonymous-object snake_case naming
        // (schema_version, scoring_version, n_pairs, predictions) consistent
        // with the inner PredictedRow record fields. Mixed casing in the same
        // file broke both LangSmithExperimentUploader (got pair_key="__") and
        // HeldoutMetricsRunner.
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public HeldoutScorer(
        IRecruiterScoringService scoring,
        ILogger<HeldoutScorer> logger)
    {
        _scoring = scoring;
        _logger = logger;
    }

    public async Task RunAsync(
        string goldPath,
        string cvDir,
        string vacancyDir,
        string outputPath,
        int concurrency = 4,
        int? limit = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(goldPath))
            throw new FileNotFoundException($"Gold file not found: {goldPath}");

        var gold = JsonSerializer.Deserialize<GoldFile>(
            await File.ReadAllTextAsync(goldPath, ct), JsonReadOpts)
            ?? throw new InvalidOperationException("Gold file is empty or malformed");

        var ratings = limit is int n ? gold.Ratings.Take(n).ToList() : gold.Ratings;
        _logger.LogInformation("Scoring {N} pairs with version {Version} (concurrency={C})",
            ratings.Count, _scoring.Version, concurrency);

        // Pre-load CV and vacancy JSON blobs as strings (the service expects raw JSON text)
        var cvCache = new Dictionary<string, string>();
        var vacCache = new Dictionary<string, string>();
        foreach (var r in ratings)
        {
            if (!cvCache.ContainsKey(r.CvId))
                cvCache[r.CvId] = await File.ReadAllTextAsync(
                    Path.Combine(cvDir, $"{r.CvId}.json"), ct);
            if (!vacCache.ContainsKey(r.VacancyId))
                vacCache[r.VacancyId] = await File.ReadAllTextAsync(
                    Path.Combine(vacancyDir, $"{r.VacancyId}.json"), ct);
        }

        var results = new List<PredictedRow>();
        var semaphore = new SemaphoreSlim(concurrency);
        var totalSw = Stopwatch.StartNew();
        var done = 0;
        var totalIn = 0L;
        var totalOut = 0L;

        var tasks = ratings.Select(async r =>
        {
            await semaphore.WaitAsync(ct);
            try
            {
                // Fresh-vacancy IDs from the work.ua/djinni scraper look like
                // "workua_6012309" rather than UUIDs. The scoring service's API
                // takes Guid as an opaque cache key — derive a deterministic GUID
                // from the string ID (MD5-based v3-style) when the ID isn't a UUID.
                Guid vacGuid;
                if (!Guid.TryParse(r.VacancyId, out vacGuid))
                {
                    using var md5 = System.Security.Cryptography.MD5.Create();
                    var hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(r.VacancyId));
                    vacGuid = new Guid(hash);
                }
                var sw = Stopwatch.StartNew();
                var result = await _scoring.ScoreAsync(
                    r.CvId, vacGuid, cvCache[r.CvId], vacCache[r.VacancyId], ct);
                sw.Stop();

                // Gemini 2.5 Flash pricing (approximate, June 2026): $0.30/M input, $2.50/M output
                var cost = result.InputTokens * 0.30 / 1_000_000.0
                         + result.OutputTokens * 2.50 / 1_000_000.0;
                lock (results)
                {
                    results.Add(new PredictedRow
                    {
                        CvId = r.CvId,
                        VacancyId = r.VacancyId,
                        Gold = r.MatchQuality,
                        GoldNorm = r.MatchQuality / 10.0,
                        PredictedScore = result.Score,
                        SubScores = new
                        {
                            skill_match       = result.SubScores.SkillMatch,
                            seniority_match   = result.SubScores.SeniorityMatch,
                            experience_match  = result.SubScores.ExperienceMatch,
                            language_match    = result.SubScores.LanguageMatch,
                            education_match   = result.SubScores.EducationMatch,
                            role_intent_match = result.SubScores.RoleIntentMatch,
                            domain_alignment  = result.SubScores.DomainAlignment
                        },
                        AntiFlagPenalty = result.AntiFlagPenalty,
                        TriggeredAntiFlags = result.Evidence?.TriggeredAntiFlags?.ToList() ?? new List<string>(),
                        Confidence = result.Confidence,
                        ModelVersion = result.ModelVersion,
                        ReasonEn = result.ReasonEn ?? "",
                        ReasonUk = result.ReasonUk ?? "",
                        Verdict = result.Verdict.ToString(),
                        InputTokens = result.InputTokens,
                        OutputTokens = result.OutputTokens,
                        EstimatedCostUsd = Math.Round(cost, 6),
                        LatencyMs = sw.ElapsedMilliseconds
                    });
                    Interlocked.Add(ref totalIn,  result.InputTokens);
                    Interlocked.Add(ref totalOut, result.OutputTokens);
                    var d = Interlocked.Increment(ref done);
                    if (d % 10 == 0)
                        _logger.LogInformation("  ... {Done}/{Total} pairs scored", d, ratings.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Score failed for {Cv}×{Vac}", r.CvId, r.VacancyId);
            }
            finally { semaphore.Release(); }
        });

        await Task.WhenAll(tasks);
        totalSw.Stop();

        var output = new
        {
            schema_version = "heldout_predictions_v1",
            generated_at = DateTime.UtcNow.ToString("O"),
            scoring_version = _scoring.Version,
            n_pairs = results.Count,
            wall_clock_seconds = Math.Round(totalSw.Elapsed.TotalSeconds, 2),
            total_input_tokens = totalIn,
            total_output_tokens = totalOut,
            predictions = results.OrderBy(r => r.CvId).ThenBy(r => r.VacancyId).ToList()
        };
        var outDir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(outputPath,
            JsonSerializer.Serialize(output, JsonWriteOpts), ct);
        _logger.LogInformation(
            "Saved {N} predictions to {Out}. Tokens in={In} out={Out} time={Sec}s",
            results.Count, outputPath, totalIn, totalOut,
            Math.Round(totalSw.Elapsed.TotalSeconds, 1));
    }

    // ── Internal DTOs ──────────────────────────────────────────────────

    private sealed record GoldFile(
        string SchemaVersion,
        string Rater,
        List<GoldRating> Ratings);

    private sealed record GoldRating(
        string CvId,
        string VacancyId,
        int MatchQuality);

    private sealed record PredictedRow
    {
        public string CvId { get; init; } = "";
        public string VacancyId { get; init; } = "";
        public int Gold { get; init; }
        public double GoldNorm { get; init; }
        public double PredictedScore { get; init; }
        public object SubScores { get; init; } = new();
        public double AntiFlagPenalty { get; init; }
        public List<string> TriggeredAntiFlags { get; init; } = new();
        public double Confidence { get; init; }
        public string ModelVersion { get; init; } = "";
        public string ReasonEn { get; init; } = "";
        public string ReasonUk { get; init; } = "";
        public string Verdict { get; init; } = "";
        public int InputTokens { get; init; }
        public int OutputTokens { get; init; }
        public double EstimatedCostUsd { get; init; }
        public long LatencyMs { get; init; }
    }
}
