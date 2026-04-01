namespace ReadyStackGo.Application;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Application.UseCases.Deployments.Precheck.Rules;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Register MediatR and all handlers from this assembly
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));

        // Domain event dispatch through MediatR
        services.AddScoped<IDomainEventDispatcher, MediatRDomainEventDispatcher>();

        // Deployment Precheck Rules — Application-layer rules (v0.59)
        services.AddScoped<IDeploymentPrecheckRule, VariableValidationRule>();
        services.AddScoped<IDeploymentPrecheckRule, ExistingDeploymentRule>();

        // Distribution extension points (TryAdd: downstream distributions can register their own before calling AddApplication)
        services.TryAddSingleton<ISetupWizardDefinitionProvider, GenericSetupWizardDefinitionProvider>();
        services.TryAddScoped<IBootstrapper, GenericBootstrapper>();

        return services;
    }
}
