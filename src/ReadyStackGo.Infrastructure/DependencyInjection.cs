using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Infrastructure.Caching;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.DataAccess;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Parsing;
using ReadyStackGo.Infrastructure.Security;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Services.Deployment;
using ReadyStackGo.Infrastructure.Services.Health;
using ReadyStackGo.Infrastructure.Services.StackSources;
using ReadyStackGo.Infrastructure.Tls;

namespace ReadyStackGo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Add DataAccess (EF Core, SQLite, Repositories)
        services.AddDataAccess(configuration);

        // Add Security (JWT, BCrypt, Password Hashing)
        services.AddSecurity(configuration);

        // Add Docker (DockerService, DockerCompose parsing)
        services.AddDocker();

        // TLS services
        services.AddSingleton<ITlsService, TlsService>();

        // Configuration services
        services.AddSingleton<IConfigStore, ConfigStore>();
        services.AddSingleton<ISystemConfigService, SystemConfigService>();
        services.AddSingleton<IWizardTimeoutService, WizardTimeoutService>();

        // RSGo Manifest services
        services.AddSingleton<IManifestProvider, ManifestProvider>();
        services.AddSingleton<IRsgoManifestParser, RsgoManifestParser>();

        // Registry credential provider for Docker image pulls
        services.AddScoped<IRegistryCredentialProvider, RegistryCredentialProvider>();

        // Deployment services
        services.AddScoped<IDeploymentEngine, DeploymentEngine>();
        services.AddScoped<IDeploymentService, DeploymentService>();

        // Product source services
        services.AddSingleton<IProductCache, InMemoryProductCache>();
        services.AddSingleton<IProductSourceProvider, LocalDirectoryProductSourceProvider>();
        services.AddSingleton<IProductSourceProvider, GitRepositoryProductSourceProvider>();
        services.AddSingleton<IProductSourceService, ProductSourceService>();

        // Health Monitoring (v0.11)
        services.AddScoped<IHealthMonitoringService, HealthMonitoringService>();
        services.AddScoped<IHealthCollectorService, HealthCollectorService>();

        // Maintenance Observers (v0.11)
        services.AddSingleton<IMaintenanceObserverFactory, MaintenanceObserverFactory>();
        services.AddScoped<IMaintenanceObserverService, MaintenanceObserverService>();

        // HTTP client for HTTP observer
        services.AddHttpClient("MaintenanceObserver", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-MaintenanceObserver");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        // HTTP Health Checker for ASP.NET Core /hc endpoints
        services.AddHttpClient<IHttpHealthChecker, HttpHealthChecker>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-HealthChecker");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        // Domain Services
        services.AddScoped<SystemAdminRegistrationService>();
        services.AddScoped<OrganizationProvisioningService>();
        services.AddScoped<AuthenticationService>();

        return services;
    }
}
