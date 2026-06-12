using System.Text.Json;
using Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace EvalTool.LangSmith;

/// <summary>
/// Step-2B orchestrator: reads the output of `score-heldout`
/// (predicted composite + sub-scores per CV×vacancy pair) together with the
/// local <c>langsmith_example_map.json</c> produced by
/// <see cref="LangSmithDatasetUploader"/>, then:
///
/// <list type="number">
///   <item>Creates a LangSmith Session (Experiment) with
///         <c>reference_dataset_id</c> = our held-out dataset.</item>
///   <item>For each predicted row, posts a <c>POST /runs</c> linked to its
///         dataset example via <c>reference_example_id</c> + <c>session_id</c>.</item>
///   <item>Posts <c>POST /feedback</c> with key=<c>abs_error</c> per run for
///         server-side aggregation in the LangSmith UI.</item>
///   <item>Closes the session via <c>PATCH /sessions/{id}</c>.</item>
/// </list>
///
/// Result: a publication-quality LangSmith Experiment row in
/// <c>https://smith.langchain.com → Datasets → vakansio_match_quality_heldout →
/// Experiments</c> with 131 rows side-by-side (predicted vs gold). Screenshot
/// goes in the thesis appendix.
/// </summary>
public sealed class LangSmithExperimentUploader
{
    private readonly LangSmithDatasetClient _client;
    private readonly ILogger<LangSmithExperimentUploader> _logger;

    private const string DatasetName = "vakansio_match_quality_heldout";

    private static readonly JsonSerializerOptions JsonReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private static readonly JsonSerializerOptions JsonReadSnakeOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    public LangSmithExperimentUploader(
        LangSmithDatasetClient client,
        ILogger<LangSmithExperimentUploader> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(
        string predictionsPath,
        string mappingPath,
        string? experimentName,
        string description,
        CancellationToken ct = default)
    {
        if (!File.Exists(predictionsPath))
            throw new FileNotFoundException("Predictions not found", predictionsPath);
        if (!File.Exists(mappingPath))
            throw new FileNotFoundException(
                "LangSmith example map not found — run upload-langsmith-dataset first", mappingPath);

        var preds = JsonSerializer.Deserialize<PredictionsFile>(
                        await File.ReadAllTextAsync(predictionsPath, ct), JsonReadSnakeOpts)
                    ?? throw new InvalidOperationException("Empty predictions");
        var mapping = JsonSerializer.Deserialize<MappingFile>(
                          await File.ReadAllTextAsync(mappingPath, ct), JsonReadSnakeOpts)
                      ?? throw new InvalidOperationException("Empty mapping");

        var promptVersion = preds.ScoringVersion ?? "unknown";
        var rows = preds.Predictions;
        _logger.LogInformation("Loaded {N} predictions, version={V}", rows.Count, promptVersion);
        _logger.LogInformation("  {N} pair_key → example_id mappings", mapping.Mapping.Count);

        var expName = experimentName
            ?? $"vakansio_{promptVersion}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

        _logger.LogInformation("[1/4] Finding dataset '{Name}'...", DatasetName);
        var ds = await _client.FindDatasetByNameAsync(DatasetName, ct)
                  ?? throw new InvalidOperationException(
                      $"Dataset '{DatasetName}' not found. Run upload-langsmith-dataset first.");

        _logger.LogInformation("[2/4] Creating Experiment session '{Exp}'...", expName);
        var sessionId = await _client.CreateSessionAsync(
            name: expName,
            description: description,
            referenceDatasetId: ds.Id,
            metadata: new Dictionary<string, object?>
            {
                ["prompt_version"]    = promptVersion,
                ["system"]            = "vakansio_recruiter_scoring",
                ["n_pairs"]           = rows.Count,
                ["uploaded_at"]       = DateTime.UtcNow
            },
            ct: ct);

        _logger.LogInformation(
            "[3/4] Posting {N} runs (each linked to its dataset example)...", rows.Count);
        var posted = 0;
        var feedbackQueue = new List<(Guid runId, double absError)>(rows.Count);
        foreach (var row in rows)
        {
            var pairKey = $"{row.CvId}__{row.VacancyId}";
            if (!mapping.Mapping.TryGetValue(pairKey, out var exampleIdStr)
                || !Guid.TryParse(exampleIdStr, out var exampleId))
            {
                _logger.LogWarning("No example mapping for {Pair} — skipping", pairKey);
                continue;
            }
            try
            {
                var startTime = DateTime.UtcNow.AddMilliseconds(-row.LatencyMs);
                var endTime   = DateTime.UtcNow;
                var inputs = new Dictionary<string, object?>
                {
                    ["cv_id"]      = row.CvId,
                    ["vacancy_id"] = row.VacancyId
                };
                var outputs = new Dictionary<string, object?>
                {
                    ["predicted_score"]      = row.PredictedScore,
                    ["sub_scores"]           = row.SubScores,
                    ["anti_flag_penalty"]    = row.AntiFlagPenalty,
                    ["triggered_anti_flags"] = row.TriggeredAntiFlags,
                    ["confidence"]           = row.Confidence,
                    ["verdict"]              = row.Verdict,
                    ["reason_en"]            = row.ReasonEn,
                    ["model_version"]        = row.ModelVersion,
                    ["input_tokens"]         = row.InputTokens,
                    ["output_tokens"]        = row.OutputTokens,
                    ["latency_ms"]           = row.LatencyMs,
                    ["estimated_cost_usd"]   = row.EstimatedCostUsd
                };
                var meta = new Dictionary<string, object?>
                {
                    ["gold"]      = row.Gold,
                    ["gold_norm"] = row.GoldNorm
                };
                var runId = await _client.PostRunAsync(
                    sessionId: sessionId,
                    referenceExampleId: exampleId,
                    runName: "recruiter_monolithic_scoring",
                    runType: "llm",
                    inputs: inputs,
                    outputs: outputs,
                    startTime: startTime,
                    endTime: endTime,
                    metadata: meta,
                    ct: ct);

                // LangSmith /feedback enforces max-4-decimal precision on `score`.
                // Without explicit rounding, double-arithmetic noise (e.g. 0.075599999…)
                // trips HTTP 422 "score has a maximum precision of 4 decimal places".
                var absErr = Math.Round(Math.Abs(row.PredictedScore - row.GoldNorm), 4);
                feedbackQueue.Add((runId, absErr));
                posted++;
                if (posted % 20 == 0)
                    _logger.LogInformation("  ... {Done}/{Total} runs posted", posted, rows.Count);
                await Task.Delay(40, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to post run for {Pair}", pairKey);
            }
        }

        _logger.LogInformation("[4/4] Posting {N} feedback rows...", feedbackQueue.Count);
        var fbOk = 0;
        foreach (var (runId, absErr) in feedbackQueue)
        {
            try
            {
                await _client.PostFeedbackAsync(
                    runId: runId,
                    key: "abs_error",
                    score: absErr,
                    value: absErr.ToString("F3"),
                    comment: "|predicted − gold_norm|",
                    ct: ct);
                fbOk++;
                await Task.Delay(20, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Feedback POST failed for run {RunId}", runId);
            }
        }

        await _client.CloseSessionAsync(sessionId, ct);

        _logger.LogInformation(
            "✓ Experiment '{Exp}' uploaded. Runs={Posted}/{Total} Feedback={Fb}/{FbTotal}",
            expName, posted, rows.Count, fbOk, feedbackQueue.Count);
        _logger.LogInformation(
            "URL: https://smith.langchain.com → Datasets → {Ds} → Experiments → {Exp}",
            DatasetName, expName);
    }

    // ── DTOs (snake_case JSON via naming policy reads camelCase too via case-insensitive) ────

    private sealed record PredictionsFile(
        string SchemaVersion,
        string GeneratedAt,
        string? ScoringVersion,
        int NPairs,
        List<PredictionRow> Predictions);

    private sealed record PredictionRow(
        string CvId,
        string VacancyId,
        int Gold,
        double GoldNorm,
        double PredictedScore,
        JsonElement SubScores,
        double AntiFlagPenalty,
        List<string>? TriggeredAntiFlags,
        double Confidence,
        string ModelVersion,
        string ReasonEn,
        string ReasonUk,
        string Verdict,
        int InputTokens,
        int OutputTokens,
        double EstimatedCostUsd,
        long LatencyMs);

    private sealed record MappingFile(
        string DatasetId,
        string DatasetName,
        int NExamplesTotal,
        Dictionary<string, string> Mapping);
}
