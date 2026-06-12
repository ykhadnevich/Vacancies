using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Infrastructure.JobSources.Api;
using Infrastructure.JobSources.Rss;
using Infrastructure.JobSources.Scraping;
using Infrastructure.MlApi;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.RelevancePipeline;
using Infrastructure.RelevancePipeline.FamilyCaps;
using Infrastructure.RelevancePipeline.Prompts.V2;
using Infrastructure.RelevancePipeline.V2.CvNormalization;
using Infrastructure.RelevancePipeline.Stage1_PreFilter;
using Infrastructure.Workers;
using Infrastructure.Deduplication;
using Infrastructure.JobSources;
using Infrastructure.Services;
using Infrastructure.Observability;
using Application.Common.Observability;
using Amazon.S3;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {


        services.AddMemoryCache(opts => opts.SizeLimit = 1000);

        // LangSmithTracer MUST be a Singleton — same instance must serve both
        // ILlmTracer (producer) and IHostedService (background channel pump).
        var langSmithKey = configuration["LangSmith:ApiKey"];
        var langSmithEnabled = !string.IsNullOrWhiteSpace(langSmithKey)
                            && (configuration["LangSmith:Enabled"]?.ToLowerInvariant() != "false");
        if (langSmithEnabled)
        {
            services.AddHttpClient();
            services.AddSingleton<LangSmithTracer>(sp =>
            {
                var http = sp.GetRequiredService<IHttpClientFactory>().CreateClient("LangSmith");
                return new LangSmithTracer(
                    http,
                    sp.GetRequiredService<IConfiguration>(),
                    sp.GetRequiredService<ILogger<LangSmithTracer>>());
            });
            services.AddSingleton<ILlmTracer>(sp => sp.GetRequiredService<LangSmithTracer>());
            services.AddHostedService(sp => sp.GetRequiredService<LangSmithTracer>());
        }
        else
        {
            services.AddSingleton<ILlmTracer>(NoopLlmTracer.Instance);
        }


        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(
                configuration.GetConnectionString("DefaultConnection"),
                o => o.UseVector()));


        services.Configure<S3StorageOptions>(
            configuration.GetSection(S3StorageOptions.SectionName));
        var s3Bucket = configuration[$"{S3StorageOptions.SectionName}:CvBucket"];
        if (!string.IsNullOrWhiteSpace(s3Bucket))
        {
            var s3Region = configuration[$"{S3StorageOptions.SectionName}:Region"]
                ?? "eu-central-1";
            services.AddSingleton<IAmazonS3>(_ => new AmazonS3Client(
                Amazon.RegionEndpoint.GetBySystemName(s3Region)));
            services.AddScoped<ICvFileStorage, S3CvFileStorage>();
        }
        else
        {
            services.AddSingleton<ICvFileStorage, NoOpCvFileStorage>();
        }


        services.AddScoped<IJobVacancyRepository, JobVacancyRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<ISavedUrlRepository, SavedUrlRepository>();
        services.AddScoped<IDeduplicationService, DeduplicationService>();


        services.AddScoped<ISkillVocabularyRepository, SkillVocabularyRepository>();
        services.AddScoped<ISkillVocabularyService,
            Application.Common.SkillVocabulary.SkillVocabularyService>();


        services.AddScoped<IScoringCacheRepository, ScoringCacheRepository>();


        services.AddScoped<ICandidateListRepository, CandidateListRepository>();
        services.AddScoped<IRecruiterCandidateRepository, RecruiterCandidateRepository>();
        services.AddScoped<ICandidateScoreRepository, CandidateScoreRepository>();

        services.AddScoped<IUserSearchSnapshotRepository, UserSearchSnapshotRepository>();


        services.AddScoped<IGeminiCostLogRepository, GeminiCostLogRepository>();
        services.AddScoped<ICostLogService, CostLogService>();

        services.AddScoped<IAuditEntryRepository, AuditEntryRepository>();


        services.AddHttpClient<JobDescriptionFetcher>();
        services.AddHttpClient<RobotaUaApiService>();
        services.AddHttpClient<LinkedInGuestService>();
        services.AddHttpClient<WorkUaScraperService>();
        services.AddHttpClient<DjinniScraperService>();
        services.AddHttpClient<ManualUrlScraperService>();
        services.AddHttpClient<IRecruiterVacancyScraper, RecruiterVacancyScraperService>();
        services.AddHttpClient<IJobDescriptionFetcher, JobDescriptionFetcher>();
        services.AddHttpClient<JoobleApiService>();

        services.AddScoped<IJobDescriptionFetcher, JobDescriptionFetcher>();
        services.AddScoped<IJobSourceService, RobotaUaApiService>();
        services.AddScoped<IJobSourceService, JoobleApiService>();
        services.AddScoped<IJobSourceService, DouRssFeedService>();
        services.AddScoped<IJobSourceService, LinkedInGuestService>();
        services.AddScoped<IJobSourceService, WorkUaScraperService>();
        services.AddScoped<IJobSourceService, DjinniScraperService>();
        services.AddScoped<IJobSourceService, ManualUrlScraperService>();

        services.AddScoped<ICvParserService, CvParserService>();


        services.Configure<MlApiOptions>(
            configuration.GetSection(MlApiOptions.SectionName));

        var mlBaseUrl = configuration[$"{MlApiOptions.SectionName}:BaseUrl"]
            ?? "http://localhost:8000";
        var mlApiKey = configuration[$"{MlApiOptions.SectionName}:ApiKey"]
            ?? string.Empty;
        var mlTimeout = int.TryParse(
            configuration[$"{MlApiOptions.SectionName}:TimeoutSeconds"], out var t) ? t : 30;
        var mlScoringTimeout = int.TryParse(
            configuration[$"{MlApiOptions.SectionName}:ScoringTimeoutSeconds"], out var st) ? st : 120;


        services.AddHttpClient<IRelevanceScoringService, MlApiScoringService>(client =>
        {
            client.BaseAddress = new Uri(mlBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(mlScoringTimeout);
            if (!string.IsNullOrEmpty(mlApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", mlApiKey);
        });


        services.AddHttpClient<IReasoningService, MlApiReasoningService>(client =>
        {
            client.BaseAddress = new Uri(mlBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(120);
            if (!string.IsNullOrEmpty(mlApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", mlApiKey);
        });


        services.AddHttpClient<IEmbeddingService, MlApiEmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(mlBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(mlTimeout);
            if (!string.IsNullOrEmpty(mlApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", mlApiKey);
        });


        services.AddHttpClient<MlApiEmbeddingService>(client =>
        {
            client.BaseAddress = new Uri(mlBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(mlTimeout);
            if (!string.IsNullOrEmpty(mlApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", mlApiKey);
        });


        services.AddHttpClient<IFactualityCheckService, MlApiFactualityService>(client =>
        {
            client.BaseAddress = new Uri(mlBaseUrl);
            client.Timeout = TimeSpan.FromSeconds(180);
            if (!string.IsNullOrEmpty(mlApiKey))
                client.DefaultRequestHeaders.Add("X-API-Key", mlApiKey);
        });


        services.AddScoped<IReasoningCacheService, ReasoningCacheService>();


        services.AddHttpClient<IGeminiScoringService, GeminiScoringService>();


        services.AddScoped<GeminiReasoningProvider>();
        services.AddScoped<GroqReasoningProvider>();
        services.AddScoped<NoOpReasoningProvider>();
        services.AddScoped<IJobReasoningServiceFactory, JobReasoningServiceFactory>();


        services.AddScoped<IReasoningContext, ReasoningContext>();


        services.AddHttpClient<ICvExtractionService, GeminiCvNormalizationService>();


        services.AddHttpClient<IVacancyExtractionService, GeminiVacancyNormalizationService>();


        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.TechVacancyNormalizationModule>();


        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.MarketingVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.SalesVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.HrVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.HealthcareVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.ProductVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModule,
            RelevancePipeline.V2.VacancyNormalization.GenericVacancyNormalizationModule>();
        services.AddSingleton<IVacancyNormalizationModuleResolver>(sp =>
            new RelevancePipeline.V2.VacancyNormalization.VacancyNormalizationModuleResolver(
                sp.GetServices<IVacancyNormalizationModule>()));
        services.AddSingleton<IVacancyDomainRouter,
            RelevancePipeline.V2.VacancyNormalization.KeywordVacancyDomainRouter>();
        services.AddSingleton<IVacancyNormalizationPromptBuilder,
            RelevancePipeline.V2.VacancyNormalization.VacancyNormalizationPromptBuilder>();
        services.AddSingleton<IVacancyNormalizationPostProcessor,
            RelevancePipeline.V2.VacancyNormalization.VacancyNormalizationPostProcessor>();


        services.AddSingleton<ICvNormalizationModule, TechCvNormalizationModule>();
        services.AddSingleton<ICvNormalizationModule, ProductCvNormalizationModule>();
        services.AddSingleton<ICvNormalizationModule, GenericCvNormalizationModule>();
        services.AddSingleton<ICvNormalizationModuleResolver>(sp =>
            new CvNormalizationModuleResolver(sp.GetServices<ICvNormalizationModule>()));
        services.AddSingleton<ICvDomainRouter, KeywordCvDomainRouter>();
        services.AddSingleton<ICvNormalizationPromptBuilder, CvNormalizationPromptBuilder>();


        services.AddSingleton<ICvNormalizationPostProcessor, CvNormalizationPostProcessor>();


        services.AddScoped<IPreFilterService, PreFilterService>();


        services.AddSingleton<IExperienceCapService, ExperienceCapService>();
        services.AddScoped<IRelevancePipeline, RelevancePipelineService>();


        services.AddSingleton<IScoringModule, PmScoringModule>();
        services.AddSingleton<IScoringModule, GenericScoringModule>();
        services.AddSingleton<IScoringModule, EngineeringScoringModule>();
        services.AddSingleton<IScoringModule, DataScoringModule>();
        services.AddSingleton<IScoringModule, DesignScoringModule>();
        services.AddSingleton<IScoringModuleResolver>(sp =>
            new ScoringModuleResolver(
                sp.GetServices<IScoringModule>(),
                requireGeneric: true ));


        services.AddSingleton<IRoleRouter, KeywordRoleRouter>();
        services.AddSingleton<SlotComposer>();
        services.AddSingleton<IScoringPromptBuilder, ScoringPromptBuilder>();
        services.AddSingleton<PmFamilyCaps>();
        services.AddSingleton<EngFamilyCaps>();


        if (configuration.GetValue<bool>("Ml:EnableEmbeddingWorker", false))
            services.AddHostedService<JobEmbeddingWorker>();


        if (configuration.GetValue<bool>("BackgroundWorkers:EnableCvSummary", false))
            services.AddHostedService<CvSummaryWorker>();
        if (configuration.GetValue<bool>("BackgroundWorkers:EnableVacancyAnalysis", false))
            services.AddHostedService<VacancyAnalysisWorker>();


        services.AddScoped<IJobAggregationService,
            JobAggregation.JobAggregationService>();


        services.AddSingleton<IEvalIterationReader,
            RelevancePipeline.V2.Eval.EvalIterationReader>();


        services.AddSingleton<IEvalDataSource,
            RelevancePipeline.V2.Eval.FileSystemEvalDataSource>();


        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.SkillMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.SeniorityMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.ExperienceMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.LanguageMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.EducationMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.RoleIntentMatchCalculator>();
        services.AddSingleton<ISubScoreCalculator,
            RelevancePipeline.V2.Scoring.SubScoreCalculators.DomainAlignmentCalculator>();
        // Pick which scoring engine is wired behind IScoringService based on
        // the `Scoring:Engine` setting ("linear" | "mono"). The other engine
        // stays available as a concrete type so EvalTool and tests can still
        // resolve it explicitly.
        var scoringEngine = (configuration["Scoring:Engine"] ?? "linear")
            .Trim().ToLowerInvariant();
        if (scoringEngine == "mono")
        {
            services.AddHttpClient<IScoringService,
                RelevancePipeline.V2.Scoring.Monolithic.MonolithicScoringService>();
            services.AddHttpClient<RelevancePipeline.V2.Scoring.ScoringServiceV2>();
        }
        else
        {
            services.AddHttpClient<IScoringService,
                RelevancePipeline.V2.Scoring.ScoringServiceV2>();
            services.AddHttpClient<RelevancePipeline.V2.Scoring.Monolithic.MonolithicScoringService>();
        }


        services.AddHttpClient<ICompositeJudgeService,
            RelevancePipeline.V2.Scoring.GeminiCompositeJudgeService>();
        services.AddSingleton<IScoringCapService,
            RelevancePipeline.V2.Scoring.ScoringCapService>();


        services.AddHttpClient<ISkillExpansionService,
            RelevancePipeline.V2.SkillExpansion.GeminiSkillExpansionService>();


        services.AddHttpClient<IBatchSkillExpander,
            RelevancePipeline.V2.SkillExpansion.GeminiBatchSkillExpander>();


        services.AddHttpClient<IBatchedReasonService,
            RelevancePipeline.V2.Scoring.GeminiBatchedReasonService>();


        services.AddHttpClient<IBatchedVacancyExtractionService,
            Services.GeminiBatchedVacancyExtractionService>();


        services.AddHttpClient<IBatchedJudgeService,
            RelevancePipeline.V2.Scoring.GeminiBatchedJudgeService>();


        services.AddHttpClient<IRecruiterScoringService,
            RelevancePipeline.V2.Scoring.Monolithic.RecruiterMonolithicScoringService>();


        // Loaded once at startup; falls back to NoopScoreCalibrator when path is empty/missing.
        services.AddSingleton<IScoreCalibrator>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();
            var path = config["Calibration:RecruiterPath"];
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger(typeof(Calibration.CalibratorLoader).FullName!);
            return Calibration.CalibratorLoader.LoadOrNoop(path, logger);
        });

        return services;
    }
}
