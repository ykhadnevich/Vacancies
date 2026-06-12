using EvalTool;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;


var builder = Host.CreateApplicationBuilder(args);


builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
});
builder.Logging.SetMinimumLevel(LogLevel.Warning);
builder.Logging.AddFilter("EvalTool", LogLevel.Information);
builder.Logging.AddFilter("Infrastructure.Services", LogLevel.Information);
builder.Logging.AddFilter("Infrastructure.JobSources", LogLevel.Information);


builder.Configuration.SetBasePath(AppContext.BaseDirectory);
builder.Configuration.AddJsonFile("appsettings.json", optional: false);
builder.Configuration.AddJsonFile("appsettings.Local.json", optional: true);
builder.Configuration.AddEnvironmentVariables();


builder.Services.AddEvalToolServices(builder.Configuration);

var host = builder.Build();


string command = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "evaluate";
string goldSetPath = ParseArg(args, "--gold-set") ?? "gold_set";


string outputDir   = ParseArg(args, "--output")
    ?? Path.Combine("results", "run_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
string version     = ParseArg(args, "--version") ?? "baseline";
int samples        = int.TryParse(ParseArg(args, "--samples"), out var s) && s >= 1 ? s : 1;


goldSetPath = Path.GetFullPath(goldSetPath);
outputDir   = Path.GetFullPath(outputDir);

try
{
    if (command == "evaluate")
    {
        var orchestrator = host.Services.GetRequiredService<EvalOrchestrator>();
        var overall = await orchestrator.RunAsync(goldSetPath, outputDir, version, samples: samples);
        Environment.ExitCode = overall >= 0.5 ? 0 : 1;
    }
    else if (command == "scrape")
    {
        string queriesFile = ParseArg(args, "--queries-file")
            ?? "docs/phase2-handoff/queries.txt";
        string scrapeOutput = ParseArg(args, "--output")
            ?? "gold_set_v2/vacancies/raw/all_vacancies.json";
        int maxPerQuery = int.TryParse(ParseArg(args, "--max-per-query"), out var m) && m >= 1
            ? m
            : 1000;


        bool useAllSources = args.Any(a =>
            string.Equals(a, "--all-sources", StringComparison.OrdinalIgnoreCase));

        queriesFile  = Path.GetFullPath(queriesFile);
        scrapeOutput = Path.GetFullPath(scrapeOutput);

        var runner = host.Services.GetRequiredService<EvalTool.Pipeline.VacancyScrapeRunner>();
        await runner.RunAsync(queriesFile, scrapeOutput, maxPerQuery, useAllSources);
        Environment.ExitCode = 0;
    }
    else if (command == "run-scoring")
    {


        string cvGoldDir = ParseArg(args, "--cv-gold") ?? "gold_set/expected";
        string vacancyNormalizedDir = ParseArg(args, "--vacancy-normalized")
            ?? "results/vacancy_20260522_194353/normalized";
        string selectedJson = ParseArg(args, "--selected")
            ?? "gold_set_v2/vacancies/selected/selected.json";
        bool skipJudge = args.Any(a =>
            string.Equals(a, "--skip-judge", StringComparison.OrdinalIgnoreCase));
        bool skipReason = args.Any(a =>
            string.Equals(a, "--skip-reason", StringComparison.OrdinalIgnoreCase));
        string defaultRunName = (skipJudge, skipReason) switch
        {
            (true,  true)  => "scoring_linear_noreason_"  + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            (true,  false) => "scoring_linear_"           + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            (false, true)  => "scoring_composite_noreason_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            (false, false) => "scoring_composite_"        + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss")
        };
        string scoringOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", defaultRunName);

        cvGoldDir          = Path.GetFullPath(cvGoldDir);
        vacancyNormalizedDir = Path.GetFullPath(vacancyNormalizedDir);
        selectedJson       = Path.GetFullPath(selectedJson);
        scoringOutput      = Path.GetFullPath(scoringOutput);

        var scorer = host.Services.GetRequiredService<EvalTool.Pipeline.BatchScorer>();
        var stats = await scorer.RunAsync(
            cvGoldDir, vacancyNormalizedDir, selectedJson, scoringOutput,
            skipJudge: skipJudge, skipReason: skipReason);


        const double InputCostPer1M  = 0.30;
        const double OutputCostPer1M = 2.50;
        double costInput  = stats.InputTokens  / 1_000_000.0 * InputCostPer1M;
        double costOutput = stats.OutputTokens / 1_000_000.0 * OutputCostPer1M;
        double costTotal  = costInput + costOutput;
        string mode = (skipJudge, skipReason) switch
        {
            (true,  true)  => "Linear (no judge, no reason)",
            (true,  false) => "Linear + Reason (no judge)",
            (false, true)  => "Composite Judge (no reason)",
            (false, false) => "Composite Judge + Reason"
        };
        Console.WriteLine();
        Console.WriteLine($"== Run summary (run-scoring | mode: {mode}) ==");
        Console.WriteLine($"Pairs scored:      {stats.Success}");
        Console.WriteLine($"Pairs failed:      {stats.Failed}");
        Console.WriteLine($"Pairs skipped:     {stats.Skipped} (already present)");
        Console.WriteLine($"Missing CV gold:   {stats.MissingCv}");
        Console.WriteLine($"Missing vacancy:   {stats.MissingVac}");
        Console.WriteLine($"Reason fallback:   {stats.ReasonFallbackPairs} pairs (Gemini timeout → template)");
        Console.WriteLine($"Wall time:         {stats.Elapsed.TotalMinutes:F1} min");
        Console.WriteLine();
        Console.WriteLine($"== Gemini token usage (Judge + Reason combined) ==");
        Console.WriteLine($"Input tokens:      {stats.InputTokens:N0}");
        Console.WriteLine($"Output tokens:     {stats.OutputTokens:N0}");
        Console.WriteLine($"Total tokens:      {stats.InputTokens + stats.OutputTokens:N0}");
        Console.WriteLine($"Estimated cost:    ${costTotal:F4} " +
                          $"(in ${costInput:F4} + out ${costOutput:F4})");
        if (stats.Success > 0)
            Console.WriteLine($"Per-pair avg:      ${costTotal / stats.Success:F5}");
        Console.WriteLine();
        Console.WriteLine($"Output: {scoringOutput}");
        Environment.ExitCode = stats.Success > 0 ? 0 : 1;
    }
    else if (command == "run-scoring-monolithic")
    {


        string cvGoldDir = ParseArg(args, "--cv-gold") ?? "gold_set/expected";
        string rawVacanciesDir = ParseArg(args, "--raw-vacancies")
            ?? "gold_set_v2/vacancies/raw";
        string selectedJson = ParseArg(args, "--selected")
            ?? "gold_set_v2/vacancies/selected/selected.json";
        string promptVersion = ParseArg(args, "--prompt-version") ?? "v1";
        string defaultOutputName = promptVersion switch
        {
            "v3" => "scoring_monolithic_v3_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            "v2" => "scoring_monolithic_v2_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
            _    => "scoring_monolithic_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"),
        };
        string scoringOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", defaultOutputName);

        cvGoldDir         = Path.GetFullPath(cvGoldDir);
        rawVacanciesDir   = Path.GetFullPath(rawVacanciesDir);
        selectedJson      = Path.GetFullPath(selectedJson);
        scoringOutput     = Path.GetFullPath(scoringOutput);

        var scorer = host.Services.GetRequiredService<EvalTool.Pipeline.MonolithicBatchScorer>();
        var stats = await scorer.RunAsync(cvGoldDir, rawVacanciesDir, selectedJson, scoringOutput, promptVersion);
        Environment.ExitCode = stats.Success > 0 ? 0 : 1;
    }
    else if (command == "run-scoring-mixed")
    {

        string rawCvDir = ParseArg(args, "--raw-cv") ?? "gold_set/cv_raw_text";
        string normVacancyDir = ParseArg(args, "--normalized-vacancy")
            ?? "gold_set_v2/vacancies/expected";
        string selectedJson = ParseArg(args, "--selected")
            ?? "gold_set_v2/vacancies/selected/selected.json";
        string scoringOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", "scoring_mixed_rawcv_normvac_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        rawCvDir          = Path.GetFullPath(rawCvDir);
        normVacancyDir    = Path.GetFullPath(normVacancyDir);
        selectedJson      = Path.GetFullPath(selectedJson);
        scoringOutput     = Path.GetFullPath(scoringOutput);

        var scorer = host.Services.GetRequiredService<EvalTool.Pipeline.MixedBatchScorer>();
        var stats = await scorer.RunAsync(rawCvDir, normVacancyDir, selectedJson, scoringOutput);
        Environment.ExitCode = stats.Success > 0 ? 0 : 1;
    }
    else if (command == "run-scoring-legacy")
    {


        string cvGoldDir = ParseArg(args, "--cv-gold") ?? "gold_set/expected";
        string rawVacanciesDir = ParseArg(args, "--raw-vacancies")
            ?? "gold_set_v2/vacancies/raw";
        string selectedJson = ParseArg(args, "--selected")
            ?? "gold_set_v2/vacancies/selected/selected.json";
        string scoringOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", "scoring_legacy_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        cvGoldDir         = Path.GetFullPath(cvGoldDir);
        rawVacanciesDir   = Path.GetFullPath(rawVacanciesDir);
        selectedJson      = Path.GetFullPath(selectedJson);
        scoringOutput     = Path.GetFullPath(scoringOutput);

        var scorer = host.Services.GetRequiredService<EvalTool.Pipeline.LegacyBatchScorer>();
        var stats = await scorer.RunAsync(cvGoldDir, rawVacanciesDir, selectedJson, scoringOutput);
        Environment.ExitCode = stats.Success > 0 ? 0 : 1;
    }
    else if (command == "evaluate-vacancies")
    {


        string vacGold = ParseArg(args, "--gold-set") ?? "gold_set_v2";
        string vacOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", "vacancy_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        string vacVersion = ParseArg(args, "--version") ?? "baseline";

        vacGold   = Path.GetFullPath(vacGold);
        vacOutput = Path.GetFullPath(vacOutput);

        var orchestrator = host.Services.GetRequiredService<VacancyEvalOrchestrator>();
        var overall = await orchestrator.RunAsync(vacGold, vacOutput, vacVersion);
        Environment.ExitCode = overall >= 0.5 ? 0 : 1;
    }
    else if (command == "evaluate-scoring")
    {


        string scoringResultsDir = ParseArg(args, "--scoring-results")
            ?? throw new ArgumentException("--scoring-results is required for evaluate-scoring");
        string scoreCvGold = ParseArg(args, "--cv-gold") ?? "gold_set/expected";
        string scoreVacGold = ParseArg(args, "--vacancy-gold") ?? "gold_set_v2/vacancies/expected";
        string scoreOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", "scoring_eval_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        string scoreVersion = ParseArg(args, "--version")
            ?? Path.GetFileName(scoringResultsDir.TrimEnd(Path.DirectorySeparatorChar));

        scoringResultsDir = Path.GetFullPath(scoringResultsDir);
        scoreCvGold       = Path.GetFullPath(scoreCvGold);
        scoreVacGold      = Path.GetFullPath(scoreVacGold);
        scoreOutput       = Path.GetFullPath(scoreOutput);

        var orchestrator = host.Services.GetRequiredService<ScoringEvalOrchestrator>();
        var overall = await orchestrator.RunAsync(
            scoringResultsDir, scoreCvGold, scoreVacGold, scoreOutput, scoreVersion);
        Environment.ExitCode = overall >= 0.5 ? 0 : 1;
    }
    else if (command == "evaluate-reasons")
    {


        string scoringResultsDir = ParseArg(args, "--scoring-results")
            ?? throw new ArgumentException("--scoring-results is required for evaluate-reasons");
        string vacancyNormalizedDir = ParseArg(args, "--vacancy-normalized")
            ?? throw new ArgumentException("--vacancy-normalized is required for evaluate-reasons");
        string reasonCvGold = ParseArg(args, "--cv-gold") ?? "gold_set/expected";
        string reasonOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", "reason_eval_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));
        string reasonVersion = ParseArg(args, "--version")
            ?? Path.GetFileName(scoringResultsDir.TrimEnd(Path.DirectorySeparatorChar));

        scoringResultsDir    = Path.GetFullPath(scoringResultsDir);
        vacancyNormalizedDir = Path.GetFullPath(vacancyNormalizedDir);
        reasonCvGold         = Path.GetFullPath(reasonCvGold);
        reasonOutput         = Path.GetFullPath(reasonOutput);

        var orchestrator = host.Services.GetRequiredService<ReasonEvalOrchestrator>();
        var overall = await orchestrator.RunAsync(
            scoringResultsDir, reasonCvGold, vacancyNormalizedDir, reasonOutput, reasonVersion);
        Environment.ExitCode = overall >= 0.5 ? 0 : 1;
    }
    else if (command == "run-batched-reasons")
    {


        string reasonPromptVersion = ParseArg(args, "--reason-prompt-version") ?? "v7";
        bool useLegacyV6 = string.Equals(reasonPromptVersion, "v6", StringComparison.OrdinalIgnoreCase);


        string scoringResultsDir = ParseArg(args, "--scoring-results")
            ?? throw new ArgumentException("--scoring-results is required for run-batched-reasons");
        string vacancyNormalizedDir = ParseArg(args, "--vacancy-normalized")
            ?? "gold_set_v2/vacancies/expected";
        string brOutput = ParseArg(args, "--output")
            ?? Path.Combine("results", "reasons_v7_batched_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        scoringResultsDir    = Path.GetFullPath(scoringResultsDir);
        vacancyNormalizedDir = Path.GetFullPath(vacancyNormalizedDir);
        brOutput             = Path.GetFullPath(brOutput);

        var generator = host.Services.GetRequiredService<EvalTool.Pipeline.BatchReasonGenerator>();
        var stats = await generator.RunAsync(
            scoringResultsDir, vacancyNormalizedDir, brOutput,
            useLegacyV6Prompt: useLegacyV6);
        Console.WriteLine($"Reason prompt version: {(useLegacyV6 ? "v6 (legacy, rules 1-5)" : "v7 (production v6.7.2, rules 1-11)")}");
        Console.WriteLine();
        Console.WriteLine($"== Batched reasons (v7) ==");
        Console.WriteLine($"Requested:  {stats.TotalRequested}");
        Console.WriteLine($"Written:    {stats.Written}");
        Console.WriteLine($"Failed:     {stats.Failed}");
        Console.WriteLine($"Skipped:    {stats.Skipped}");
        Console.WriteLine($"MissingVac: {stats.MissingVacancy}");
        Console.WriteLine($"Elapsed:    {stats.Elapsed.TotalMinutes:F1} min");
        Console.WriteLine($"Output:     {brOutput}");
        Environment.ExitCode = stats.Written > 0 ? 0 : 1;
    }
    else if (command == "smoke-monolithic")
    {

        string cvFile = ParseArg(args, "--cv")
            ?? "gold_set/expected/14_frontend_mid_react.json";
        string vacancyFile = ParseArg(args, "--vacancy")
            ?? "gold_set_v2/vacancies/expected/459739e5-a61c-46ab-b761-282c7ede3e80.json";

        cvFile = Path.GetFullPath(cvFile);
        vacancyFile = Path.GetFullPath(vacancyFile);

        if (!File.Exists(cvFile))
        {
            Console.Error.WriteLine($"CV file not found: {cvFile}");
            Environment.ExitCode = 2;
            return;
        }
        if (!File.Exists(vacancyFile))
        {
            Console.Error.WriteLine($"Vacancy file not found: {vacancyFile}");
            Environment.ExitCode = 2;
            return;
        }

        var cvJson = await File.ReadAllTextAsync(cvFile);
        var vacancyText = await File.ReadAllTextAsync(vacancyFile);

        var scoring = host.Services.GetRequiredService<
            Infrastructure.RelevancePipeline.V2.Scoring.Monolithic.MonolithicScoringService>();

        Console.WriteLine($"=== Monolithic V3 Smoke Test ===");
        Console.WriteLine($"CV:      {Path.GetFileName(cvFile)}");
        Console.WriteLine($"Vacancy: {Path.GetFileName(vacancyFile)}");
        Console.WriteLine();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var result = await scoring.ScoreRawAsync(
            cvId: Path.GetFileNameWithoutExtension(cvFile),
            vacancyId: Guid.NewGuid(),
            cvSummaryJson: cvJson,
            vacancyRawText: vacancyText,
            promptVersion: "v3");
        sw.Stop();

        Console.WriteLine($"--- RESULT ---");
        Console.WriteLine($"Model:        {result.ModelVersion}");
        Console.WriteLine($"Latency:      {sw.ElapsedMilliseconds} ms");
        Console.WriteLine($"Tokens:       in={result.InputTokens}, out={result.OutputTokens}");
        Console.WriteLine();
        Console.WriteLine($"Final score:  {result.Score:F3}   ({result.Verdict})");
        Console.WriteLine($"Anti penalty: {result.AntiFlagPenalty:F2}");
        Console.WriteLine();
        Console.WriteLine($"Sub-scores:");
        Console.WriteLine($"  skill_match       = {result.SubScores.SkillMatch:F3}");
        Console.WriteLine($"  seniority_match   = {result.SubScores.SeniorityMatch:F3}");
        Console.WriteLine($"  experience_match  = {result.SubScores.ExperienceMatch:F3}");
        Console.WriteLine($"  language_match    = {result.SubScores.LanguageMatch:F3}");
        Console.WriteLine($"  education_match   = {result.SubScores.EducationMatch:F3}");
        Console.WriteLine($"  role_intent_match = {result.SubScores.RoleIntentMatch:F3}");
        Console.WriteLine($"  domain_alignment  = {result.SubScores.DomainAlignment:F3}");
        Console.WriteLine();
        Console.WriteLine($"Matched skills:    [{string.Join(", ", result.Evidence.MatchedSkills)}]");
        Console.WriteLine($"Missing must-have: [{string.Join(", ", result.Evidence.MissingMustHaves)}]");
        Console.WriteLine($"Anti-flags fired:  [{string.Join(", ", result.Evidence.TriggeredAntiFlags)}]");
        Console.WriteLine();
        Console.WriteLine($"Reason EN: {result.ReasonEn}");
        Console.WriteLine($"Reason UK: {result.ReasonUk}");

        Environment.ExitCode = result.ModelVersion.Contains("fallback") ? 1 : 0;
    }
    else if (command == "upload-langsmith-dataset")
    {
        // Step 2A — upload held-out gold to LangSmith as a Dataset of Examples.
        // Idempotent: existing examples (matched by metadata.pair_key) are skipped.
        // Saves local mapping pair_key → example_id for Step 2B.
        string heldoutGold = ParseArg(args, "--gold")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/per_pair_resolved.json";
        string cvDir       = ParseArg(args, "--cv-dir") ?? "gold_set/expected";
        string vacancyDir  = ParseArg(args, "--vacancy-dir") ?? "gold_set_v2/vacancies/expected";
        string mapPath     = ParseArg(args, "--mapping-out")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/langsmith_example_map.json";

        heldoutGold = Path.GetFullPath(heldoutGold);
        cvDir       = Path.GetFullPath(cvDir);
        vacancyDir  = Path.GetFullPath(vacancyDir);
        mapPath     = Path.GetFullPath(mapPath);

        var uploader = host.Services.GetRequiredService<EvalTool.LangSmith.LangSmithDatasetUploader>();
        await uploader.RunAsync(heldoutGold, cvDir, vacancyDir, mapPath);
        Environment.ExitCode = 0;
    }
    else if (command == "upload-langsmith-experiment")
    {
        // Step 2B — upload predicted scores as a LangSmith Experiment.
        // Creates a Session linked to the dataset, posts 131 Runs with
        // reference_example_id, plus per-run abs_error feedback.
        string predsPath = ParseArg(args, "--predictions")
            ?? throw new ArgumentException("--predictions <path> is required");
        string mapPath = ParseArg(args, "--mapping")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/langsmith_example_map.json";
        string? expName = ParseArg(args, "--experiment-name");
        string desc = ParseArg(args, "--description")
            ?? "Vakansio recruiter Mono scoring against held-out gold.";

        predsPath = Path.GetFullPath(predsPath);
        mapPath   = Path.GetFullPath(mapPath);

        var uploader = host.Services.GetRequiredService<EvalTool.LangSmith.LangSmithExperimentUploader>();
        await uploader.RunAsync(predsPath, mapPath, expName, desc);
        Environment.ExitCode = 0;
    }
    else if (command == "evaluate-version")
    {
        // One-shot end-to-end: score-heldout → baselines → compute-metrics →
        // ablation-caps → fit-calibration → optional comparison vs previous
        // version (with per-pair regression detection). Replaces the manual
        // six-command pipeline for routine prompt iteration.
        string versionTag = ParseArg(args, "--version")
            ?? throw new ArgumentException("--version <tag> is required (e.g. v1_7)");
        string? compareTo = ParseArg(args, "--compare-to");
        string evalHeldoutGold = ParseArg(args, "--gold")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/per_pair_resolved.json";
        string evalCvDir       = ParseArg(args, "--cv-dir") ?? "gold_set/expected";
        string evalVacancyDir  = ParseArg(args, "--vacancy-dir") ?? "gold_set_v2/vacancies/expected";
        string evalBaselinePath = ParseArg(args, "--baselines")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/baseline_predictions.json";
        string evalResultsRoot = ParseArg(args, "--results-root") ?? "results";

        evalHeldoutGold   = Path.GetFullPath(evalHeldoutGold);
        evalCvDir         = Path.GetFullPath(evalCvDir);
        evalVacancyDir    = Path.GetFullPath(evalVacancyDir);
        evalBaselinePath  = Path.GetFullPath(evalBaselinePath);
        evalResultsRoot   = Path.GetFullPath(evalResultsRoot);

        var evaluator = host.Services.GetRequiredService<EvalTool.Evaluation.VersionEvaluator>();
        await evaluator.RunAsync(versionTag, compareTo, evalHeldoutGold, evalCvDir, evalVacancyDir, evalBaselinePath, evalResultsRoot);
        Environment.ExitCode = 0;
    }
    else if (command == "fit-calibration")
    {
        // Fit post-hoc isotonic + Platt calibration on held-out predictions.
        // Picks the better of the two by 5-fold CV ECE and persists the chosen
        // calibrator as a portable JSON the production scoring service can load.
        string predsPath = ParseArg(args, "--predictions")
            ?? throw new ArgumentException("--predictions <path> is required");
        string outDir = ParseArg(args, "--output")
            ?? Path.Combine(Path.GetDirectoryName(predsPath) ?? ".",
                            "calibration_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        predsPath = Path.GetFullPath(predsPath);
        outDir    = Path.GetFullPath(outDir);

        var fitter = host.Services.GetRequiredService<EvalTool.Calibration.CalibrationFitter>();
        await fitter.RunAsync(predsPath, outDir);
        Environment.ExitCode = 0;
    }
    else if (command == "normalize-fresh")
    {
        // Variant B Layer 6 expansion — normalise raw vacancies into per-id JSON.
        string raw = ParseArg(args, "--raw")
            ?? throw new ArgumentException("--raw <path> is required");
        string outDir = ParseArg(args, "--output")
            ?? throw new ArgumentException("--output <dir> is required");
        int? limit = int.TryParse(ParseArg(args, "--limit"), out var ll) && ll >= 1 ? ll : (int?)null;
        // Optional: dedup against an existing normalised dir to skip vacancies the prompt already saw.
        string? dedup = ParseArg(args, "--dedup-against");

        raw    = Path.GetFullPath(raw);
        outDir = Path.GetFullPath(outDir);
        if (!string.IsNullOrEmpty(dedup)) dedup = Path.GetFullPath(dedup);

        var normalizer = host.Services.GetRequiredService<EvalTool.Pipeline.FreshVacancyNormalizer>();
        await normalizer.RunAsync(raw, outDir, limit, dedup);
        Environment.ExitCode = 0;
    }
    else if (command == "baselines")
    {
        // Step 1 — non-LLM baselines (TF-IDF char_wb cosine + BM25 Okapi).
        // Reads held-out gold + CV/vacancy JSONs, writes baseline_predictions.json
        // consumed by compute-metrics.
        string heldoutGold = ParseArg(args, "--gold")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/per_pair_resolved.json";
        string cvDir = ParseArg(args, "--cv-dir") ?? "gold_set/expected";
        string vacancyDir = ParseArg(args, "--vacancy-dir") ?? "gold_set_v2/vacancies/expected";
        string output = ParseArg(args, "--output")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/baseline_predictions.json";

        heldoutGold = Path.GetFullPath(heldoutGold);
        cvDir       = Path.GetFullPath(cvDir);
        vacancyDir  = Path.GetFullPath(vacancyDir);
        output      = Path.GetFullPath(output);

        var runner = host.Services.GetRequiredService<EvalTool.Baselines.BaselineRunner>();
        await runner.RunAsync(heldoutGold, cvDir, vacancyDir, output);
        Environment.ExitCode = 0;
    }
    else if (command == "ablation-caps")
    {
        // Step 3 ablation — apply ScoringCapService offline to the existing
        // heldout predictions and emit side-by-side metrics (caps OFF vs ON).
        string predsPath = ParseArg(args, "--predictions")
            ?? throw new ArgumentException("--predictions <path> is required");
        string cvDir       = ParseArg(args, "--cv-dir") ?? "gold_set/expected";
        string vacancyDir  = ParseArg(args, "--vacancy-dir") ?? "gold_set_v2/vacancies/expected";
        string outDir = ParseArg(args, "--output")
            ?? Path.Combine(Path.GetDirectoryName(predsPath) ?? ".",
                            "ablation_caps_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        predsPath  = Path.GetFullPath(predsPath);
        cvDir      = Path.GetFullPath(cvDir);
        vacancyDir = Path.GetFullPath(vacancyDir);
        outDir     = Path.GetFullPath(outDir);

        var runner = host.Services.GetRequiredService<EvalTool.Metrics.CapsAblationRunner>();
        await runner.RunAsync(predsPath, cvDir, vacancyDir, outDir);
        Environment.ExitCode = 0;
    }
    else if (command == "compute-metrics")
    {
        // Step 3 — compute Spearman/Kendall/QWK/NDCG/ECE + bootstrap CIs +
        // subset breakdown + baseline comparison, emit report.json + report.md.
        string predsPath = ParseArg(args, "--predictions")
            ?? throw new ArgumentException("--predictions <path> is required");
        string baselinesPath = ParseArg(args, "--baselines")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/baseline_predictions.json";
        string outDir = ParseArg(args, "--output")
            ?? Path.Combine(Path.GetDirectoryName(predsPath) ?? ".",
                            "metrics_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss"));

        predsPath     = Path.GetFullPath(predsPath);
        baselinesPath = Path.GetFullPath(baselinesPath);
        outDir        = Path.GetFullPath(outDir);

        var runner = host.Services.GetRequiredService<EvalTool.Metrics.HeldoutMetricsRunner>();
        await runner.RunAsync(predsPath, baselinesPath, outDir);
        Environment.ExitCode = 0;
    }
    else if (command == "score-heldout")
    {
        // Score every CV×vacancy pair in the held-out gold via the production
        // recruiter scoring service. Dumps a JSON file consumed by Step 3 metrics
        // and the LangSmith experiment uploader (Python sidecar).
        string heldoutGold = ParseArg(args, "--gold")
            ?? "gold_set_v2/match_quality_heldout/_aggregated/per_pair_resolved.json";
        string cvDir = ParseArg(args, "--cv-dir") ?? "gold_set/expected";
        string vacancyDir = ParseArg(args, "--vacancy-dir") ?? "gold_set_v2/vacancies/expected";
        string output = ParseArg(args, "--output")
            ?? Path.Combine("results", "heldout_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss") + ".json");
        int concurrency = int.TryParse(ParseArg(args, "--concurrency"), out var cc) && cc >= 1 ? cc : 4;
        int? limit = int.TryParse(ParseArg(args, "--limit"), out var ll) && ll >= 1 ? ll : (int?)null;

        heldoutGold = Path.GetFullPath(heldoutGold);
        cvDir = Path.GetFullPath(cvDir);
        vacancyDir = Path.GetFullPath(vacancyDir);
        output = Path.GetFullPath(output);

        var scorer = host.Services.GetRequiredService<EvalTool.Pipeline.HeldoutScorer>();
        await scorer.RunAsync(heldoutGold, cvDir, vacancyDir, output, concurrency, limit);
        Environment.ExitCode = 0;
    }
    else
    {
        Console.Error.WriteLine(
            $"Unknown command: '{command}'. " +
            $"Supported: evaluate, scrape, evaluate-vacancies, evaluate-scoring, evaluate-reasons, " +
            $"run-scoring, run-scoring-monolithic, run-scoring-legacy, run-scoring-mixed, run-batched-reasons, " +
            $"smoke-monolithic, score-heldout, baselines, normalize-fresh, fit-calibration, evaluate-version, upload-langsmith-dataset, upload-langsmith-experiment, compute-metrics, ablation-caps");
        Environment.ExitCode = 2;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Fatal: {ex.Message}");
    Console.Error.WriteLine(ex.StackTrace);
    Environment.ExitCode = 3;
}

static string? ParseArg(string[] args, string name)
{
    var idx = Array.IndexOf(args, name);
    if (idx < 0 || idx + 1 >= args.Length) return null;
    return args[idx + 1];
}
