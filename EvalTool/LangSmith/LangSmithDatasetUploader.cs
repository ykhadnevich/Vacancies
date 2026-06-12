using System.Text.Json;
using Infrastructure.Observability;
using Microsoft.Extensions.Logging;

namespace EvalTool.LangSmith;

/// <summary>
/// Step-2A orchestrator: reads the held-out gold (`per_pair_resolved.json`)
/// plus the per-pair CV summary + normalized vacancy JSON, and uploads each
/// pair to LangSmith as a Dataset Example.
///
/// **Idempotent:** examples already present in the dataset (matched by
/// <c>metadata.pair_key</c>) are skipped. Re-running after partial failure is safe.
///
/// Output: saves a local mapping <c>pair_key → langsmith_example_id</c> to
/// <c>gold_set_v2/match_quality_heldout/_aggregated/langsmith_example_map.json</c>
/// — consumed in Step 2B by <see cref="LangSmithExperimentUploader"/>.
/// </summary>
public sealed class LangSmithDatasetUploader
{
    private readonly LangSmithDatasetClient _client;
    private readonly ILogger<LangSmithDatasetUploader> _logger;

    private const string DatasetName = "vakansio_match_quality_heldout";
    private const string DatasetDesc =
        "Held-out CV/vacancy match-quality gold set. 131 pairs, ordinal 0/2/4/6/8/10. " +
        "Rated by Claude Opus 4.7 (Anthropic). Test-retest Spearman = 0.988.";

    private static readonly HashSet<string> SafetyCvs = new()
    {
        "3_junior_designer_career_switcher", "6_devops_junior", "6_hr_recruiter_generic",
        "8_healthcare_junior", "9_healthcare_senior", "10_legal_mid_corporate_lawyer",
        "11_education_senior_teacher", "12_finance_senior_accountant",
        "16_marketing_mid_growth", "18_academic_professor_humanities",
        "23_security_engineer_mid"
    };

    private static readonly JsonSerializerOptions JsonReadOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };
    private static readonly JsonSerializerOptions JsonWriteOpts = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public LangSmithDatasetUploader(
        LangSmithDatasetClient client,
        ILogger<LangSmithDatasetUploader> logger)
    {
        _client = client;
        _logger = logger;
    }

    public async Task RunAsync(
        string goldPath, string cvDir, string vacancyDir, string mappingOutPath,
        CancellationToken ct = default)
    {
        if (!File.Exists(goldPath))
            throw new FileNotFoundException("Gold not found", goldPath);
        var gold = JsonSerializer.Deserialize<GoldFile>(
                       await File.ReadAllTextAsync(goldPath, ct), JsonReadOpts)
                   ?? throw new InvalidOperationException("Gold file empty");

        _logger.LogInformation("Loaded {N} pairs from gold", gold.Ratings.Count);

        _logger.LogInformation("[1/3] Ensuring dataset exists...");
        var ds = await _client.EnsureDatasetAsync(DatasetName, DatasetDesc, ct);

        _logger.LogInformation("[2/3] Listing existing examples...");
        var existing = await _client.ListExamplesAsync(ds.Id, ct);
        var existingKeys = existing
            .Where(e => e.Metadata is not null && e.Metadata.ContainsKey("pair_key"))
            .ToDictionary(e => e.Metadata!["pair_key"].ToString()!, e => e.Id);
        _logger.LogInformation("  {N} examples already present", existingKeys.Count);

        var mapping = new Dictionary<string, string>(existingKeys);

        _logger.LogInformation("[3/3] Uploading missing examples...");
        var created = 0;
        var skipped = 0;
        foreach (var r in gold.Ratings)
        {
            var pairKey = $"{r.CvId}__{r.VacancyId}";
            if (existingKeys.ContainsKey(pairKey)) { skipped++; continue; }

            var cvPath = Path.Combine(cvDir, $"{r.CvId}.json");
            var vacPath = Path.Combine(vacancyDir, $"{r.VacancyId}.json");
            if (!File.Exists(cvPath) || !File.Exists(vacPath))
            {
                _logger.LogWarning("Missing files for {Pair} — skipping", pairKey);
                continue;
            }
            using var cvDoc = JsonDocument.Parse(await File.ReadAllTextAsync(cvPath, ct));
            using var vacDoc = JsonDocument.Parse(await File.ReadAllTextAsync(vacPath, ct));

            var inputs = new Dictionary<string, object?>
            {
                ["cv_summary_json"]         = JsonSerializer.Deserialize<JsonElement>(cvDoc.RootElement.GetRawText()),
                ["vacancy_normalized_json"] = JsonSerializer.Deserialize<JsonElement>(vacDoc.RootElement.GetRawText()),
                ["cv_id"]                   = r.CvId,
                ["vacancy_id"]              = r.VacancyId
            };
            var outputs = new Dictionary<string, object?>
            {
                ["expected_score"]      = r.MatchQuality,
                ["expected_score_norm"] = r.MatchQuality / 10.0,
                ["rationale"]           = r.Rationale ?? string.Empty,
                ["role_title"]          = r.RoleTitleEn ?? string.Empty,
                ["retest_score"]        = r.RetestScore
            };
            var metadata = new Dictionary<string, object?>
            {
                ["pair_key"]       = pairKey,
                ["rater"]          = "Claude Opus 4.7 (Anthropic)",
                ["design_subset"]  = SafetyCvs.Contains(r.CvId)
                                     ? "safety" : "coverage_or_strong_fit"
            };

            try
            {
                var ex = await _client.CreateExampleAsync(ds.Id, inputs, outputs, metadata, ct);
                mapping[pairKey] = ex.Id;
                created++;
                if (created % 20 == 0)
                    _logger.LogInformation("  ... {Done} uploaded (skipped {Skipped})", created, skipped);
                await Task.Delay(50, ct);  // gentle pacing
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to upload {Pair}", pairKey);
            }
        }

        _logger.LogInformation("Upload complete. Created={Created} Skipped={Skipped} Total={Total}",
            created, skipped, created + skipped);

        // Save mapping
        var doc = new Dictionary<string, object?>
        {
            ["dataset_id"]            = ds.Id,
            ["dataset_name"]          = DatasetName,
            ["n_examples_total"]      = created + skipped,
            ["n_created_this_run"]    = created,
            ["n_skipped_this_run"]    = skipped,
            ["mapping"]               = mapping
        };
        var outDir = Path.GetDirectoryName(mappingOutPath);
        if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
        await File.WriteAllTextAsync(mappingOutPath,
            JsonSerializer.Serialize(doc, JsonWriteOpts), ct);
        _logger.LogInformation("Saved mapping to {Path}", mappingOutPath);
    }

    // ── Gold DTOs (snake_case JSON via naming policy) ─────────────────────

    private sealed record GoldFile(
        string SchemaVersion,
        string Rater,
        List<GoldRating> Ratings);

    private sealed record GoldRating(
        string CvId,
        string VacancyId,
        string? RoleTitleEn,
        int MatchQuality,
        string? Rationale,
        int? RetestScore);
}
