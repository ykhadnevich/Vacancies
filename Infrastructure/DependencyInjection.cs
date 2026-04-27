using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Application.Common.Interfaces;
using Domain.Interfaces.Repositories;
using Domain.Interfaces.Services;
using Infrastructure.JobSources.Api;
using Infrastructure.JobSources.Rss;
using Infrastructure.JobSources.Scraping;
using Infrastructure.Persistence;
using Infrastructure.Persistence.Repositories;
using Infrastructure.RelevancePipeline;
using Infrastructure.RelevancePipeline.Stage1_PreFilter;
using Infrastructure.RelevancePipeline.Stage2_Embedding;
using Infrastructure.RelevancePipeline.Stage3_LlmRerank;
using Infrastructure.Deduplication;
using Infrastructure.JobSources;
using Infrastructure.Services;

namespace Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("DefaultConnection")));

        services.AddScoped<IJobVacancyRepository, JobVacancyRepository>();
        services.AddScoped<IApplicationRepository, ApplicationRepository>();
        services.AddScoped<IUserProfileRepository, UserProfileRepository>();
        services.AddScoped<IDeduplicationService, DeduplicationService>();

        services.AddHttpClient<JobDescriptionFetcher>();
        services.AddHttpClient<RobotaUaApiService>();
        services.AddHttpClient<LinkedInGuestService>();
        services.AddHttpClient<WorkUaScraperService>();
        services.AddHttpClient<DjinniScraperService>();
        services.AddHttpClient<ManualUrlScraperService>();
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
        services.AddHttpClient<IGeminiScoringService, GeminiScoringService>();
        services.AddScoped<ICvParserService, CvParserService>();
        services.AddScoped<ISavedUrlRepository, SavedUrlRepository>();

        services.AddScoped<IPreFilterService, PreFilterService>();

        services.AddHttpClient<EmbeddingService>(client =>
        {
            client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {configuration["OpenAiApiKey"]}");
        });
        services.AddScoped<IEmbeddingService, EmbeddingService>();

        services.AddHttpClient<LlmRerankService>(client =>
        {
            client.DefaultRequestHeaders.Add("Authorization",
                $"Bearer {configuration["OpenAiApiKey"]}");
        });
        services.AddScoped<ILlmRerankService, LlmRerankService>();

        services.AddScoped<IRelevancePipeline, RelevancePipelineService>();

        return services;
    }
}
