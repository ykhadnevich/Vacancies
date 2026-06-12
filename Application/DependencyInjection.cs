using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Application.Common.Behaviors;

namespace Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg =>
            cfg.RegisterServicesFromAssembly(assembly));

        services.AddValidatorsFromAssembly(assembly);

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(ValidationBehavior<,>));

        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(RequireRecruiterBehavior<,>));
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(RequireVacancyOwnershipBehavior<,>));
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(RequireCandidateListOwnershipBehavior<,>));

        // Registered last → runs innermost in the pipeline. Writes only on success.
        services.AddTransient(
            typeof(IPipelineBehavior<,>),
            typeof(AuditingBehavior<,>));

        return services;
    }
}
