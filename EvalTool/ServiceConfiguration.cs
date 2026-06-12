using Application.Common.Interfaces;
using Infrastructure.JobSources;
using Infrastructure.JobSources.Scraping;
using Infrastructure.RelevancePipeline.V2.CvNormalization;
using Infrastructure.RelevancePipeline.V2.Scoring;
using Infrastructure.RelevancePipeline.V2.Scoring.SubScoreCalculators;
using Infrastructure.RelevancePipeline.V2.VacancyNormalization;
using Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace EvalTool;


public static class ServiceConfiguration
{
    public static IServiceCollection AddEvalToolServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {

        services.AddScoped<ICvParserService, CvParserService>();


        services.AddSingleton<ICvNormalizationModule, TechCvNormalizationModule>();
        services.AddSingleton<ICvNormalizationModule, ProductCvNormalizationModule>();
        services.AddSingleton<ICvNormalizationModule, GenericCvNormalizationModule>();
        services.AddSingleton<ICvNormalizationModuleResolver>(sp =>
            new CvNormalizationModuleResolver(sp.GetServices<ICvNormalizationModule>()));
        services.AddSingleton<ICvDomainRouter, KeywordCvDomainRouter>();
        services.AddSingleton<ICvNormalizationPromptBuilder, CvNormalizationPromptBuilder>();
        services.AddSingleton<ICvNormalizationPostProcessor, CvNormalizationPostProcessor>();


        services.AddHttpClient<ICvExtractionService, GeminiCvNormalizationService>();


        services.AddHttpClient<IJobDescriptionFetcher, JobDescriptionFetcher>();
        services.AddHttpClient<WorkUaScraperService>();
        services.AddHttpClient<DjinniScraperService>();


        services.AddScoped<Domain.Interfaces.Services.IJobSourceService>(
            sp => sp.GetRequiredService<WorkUaScraperService>());
        services.AddScoped<Domain.Interfaces.Services.IJobSourceService>(
            sp => sp.GetRequiredService<DjinniScraperService>());


        services.AddSingleton<IVacancyNormalizationModule, TechVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModule, GenericVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModuleResolver>(sp =>
            new VacancyNormalizationModuleResolver(sp.GetServices<IVacancyNormalizationModule>()));
        services.AddSingleton<IVacancyDomainRouter, KeywordVacancyDomainRouter>();
        services.AddSingleton<IVacancyNormalizationPromptBuilder, VacancyNormalizationPromptBuilder>();
        services.AddSingleton<IVacancyNormalizationPostProcessor, VacancyNormalizationPostProcessor>();
        services.AddHttpClient<IVacancyExtractionService, GeminiVacancyNormalizationService>();


        services.AddSingleton<ISubScoreCalculator, SkillMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator, SeniorityMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator, ExperienceMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator, LanguageMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator, EducationMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator, RoleIntentMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator, DomainAlignmentCalculator>();
        services.AddHttpClient<IScoringService, ScoringServiceV2>();


        services.AddHttpClient<ICompositeJudgeService, GeminiCompositeJudgeService>();
        services.AddSingleton<IScoringCapService, ScoringCapService>();


        services.AddHttpClient<IBatchedReasonService, GeminiBatchedReasonService>();
        services.AddScoped<Pipeline.BatchReasonGenerator>();


        var mlBaseUrl = configuration["MlApi:BaseUrl"] ?? "http://localhost:8000";
        var mlApiKey = configuration["MlApi:ApiKey"] ?? string.Empty;
        services.AddHttpClient<Application.Common.Interfaces.IFactualityCheckService,
            Infrastructure.MlApi.MlApiFactualityService>(client =>
        {
            client.BaseAddress = new Uri(mlBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(180);
            if (!string.IsNullOrEmpty(mlApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", mlApiKey);
        });


        services.AddHttpClient<
            Infrastructure.RelevancePipeline.V2.Scoring.Monolithic.MonolithicScoringService>();


        // Recruiter-side scoring (needed for held-out evaluation via score-heldout command).
        // Maps to the SAME service used by the production API, ensuring evaluation runs on
        // identical code paths.
        services.AddHttpClient<
            Application.Common.Interfaces.IRecruiterScoringService,
            Infrastructure.RelevancePipeline.V2.Scoring.Monolithic.RecruiterMonolithicScoringService>();


        // ILlmTracer: noop by default for EvalTool. Switch to LangSmithTracer at runtime
        // by reading LangSmith:Enabled + LangSmith:ApiKey from configuration if needed.
        // Kept noop here because EvalTool batch runs would otherwise flood the LangSmith
        // free-tier trace quota on every smoke test.
        services.AddSingleton<Application.Common.Interfaces.ILlmTracer>(
            Application.Common.Observability.NoopLlmTracer.Instance);


        // Score calibrator: noop by default for EvalTool so that score-heldout
        // emits raw (un-calibrated) composites — these are exactly what the
        // next fit-calibration run needs as input. Production API loads a real
        // calibrator via Infrastructure.DependencyInjection.
        services.AddSingleton<Application.Common.Interfaces.IScoreCalibrator>(
            Application.Common.Observability.NoopScoreCalibrator.Instance);


        services.AddHttpClient<
            Infrastructure.RelevancePipeline.V2.Scoring.Legacy.LegacyScoringService>();


        services.AddHttpClient<
            Infrastructure.RelevancePipeline.V2.Scoring.Mixed.MixedScoringService>();


        services.AddSingleton<Pipeline.SelfConsistencyMerger>();
        services.AddScoped<Pipeline.BatchNormalizer>();
        services.AddScoped<Pipeline.VacancyScrapeRunner>();
        services.AddScoped<Pipeline.BatchVacancyNormalizer>();
        services.AddScoped<Pipeline.BatchScorer>();
        services.AddScoped<Pipeline.MonolithicBatchScorer>();
        services.AddScoped<Pipeline.LegacyBatchScorer>();
        services.AddScoped<Pipeline.MixedBatchScorer>();
        services.AddScoped<Grading.EvaluationEngine>();
        services.AddScoped<Grading.VacancyEvaluationEngine>();

        services.AddSingleton<Grading.ReasonClaimExtractor>();
        services.AddScoped<Grading.ReasonEvaluationEngine>();
        services.AddScoped<Pipeline.BatchReasonEvaluator>();
        services.AddScoped<ReasonEvalOrchestrator>();


        services.AddScoped<Grading.ScoringEvaluationEngine>();
        services.AddScoped<Pipeline.BatchScoringEvaluator>();
        services.AddScoped<ScoringEvalOrchestrator>();
        services.AddScoped<Reporting.ReportWriter>();
        services.AddScoped<VacancyEvalOrchestrator>();
        services.AddScoped<EvalOrchestrator>();

        // Held-out evaluation orchestrators — Step 0..3 thesis pipeline.
        services.AddScoped<Pipeline.HeldoutScorer>();

        // LangSmith management API client (separate from the fire-and-forget tracer,
        // because management calls must be synchronous and must surface errors).
        services.AddHttpClient<Infrastructure.Observability.LangSmithDatasetClient>();

        // Step 2 LangSmith orchestrators.
        services.AddScoped<LangSmith.LangSmithDatasetUploader>();
        services.AddScoped<LangSmith.LangSmithExperimentUploader>();

        // Step 3 metrics + reporting orchestrator.
        services.AddScoped<Metrics.HeldoutMetricsRunner>();

        // Step 3 caps on/off ablation orchestrator.
        services.AddScoped<Metrics.CapsAblationRunner>();

        // Step 1 non-LLM baselines (TF-IDF + BM25) — pure C# implementation.
        services.AddScoped<Baselines.BaselineRunner>();

        // Variant B (fresh-vacancy held-out expansion) — normalises ALL raw
        // vacancies in a single JSON without the selected.json filter.
        services.AddScoped<Pipeline.FreshVacancyNormalizer>();

        // Post-hoc calibration orchestrator — fits isotonic + Platt on held-out,
        // persists best calibrator JSON for production scoring service to load.
        services.AddScoped<Calibration.CalibrationFitter>();

        // One-shot end-to-end version evaluator with regression-pair detection
        // — replaces the manual six-command sequence with a single
        // `evaluate-version --version <tag> [--compare-to <prev_tag>]` call.
        services.AddScoped<Evaluation.VersionEvaluator>();

        return services;
    }
}
