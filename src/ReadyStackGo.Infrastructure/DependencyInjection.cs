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
using ReadyStackGo.Infrastructure.LetsEncrypt;

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
        services.AddSingleton<ITlsConfigService, TlsConfigService>();

        // Let's Encrypt services
        services.AddSingleton<IPendingChallengeStore, InMemoryPendingChallengeStore>();
        services.AddSingleton<ManualDnsProvider>();
        services.AddSingleton<IDnsProviderFactory, DnsProviderFactory>();
        services.AddScoped<ILetsEncryptService, LetsEncryptService>();
        services.AddHttpClient("Cloudflare");

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
        services.AddScoped<IProductSourceService, DatabaseProductSourceService>();

        // Source Registry (v0.24)
        services.AddSingleton<ISourceRegistryService, SourceRegistryService>();

        // Image Reference Extraction (v0.25)
        services.AddSingleton<IImageReferenceExtractor, ImageReferenceExtractor>();

        // Registry Access Checker (v0.25) — checks anonymous pull via Docker v2 API
        services.AddScoped<IRegistryAccessChecker, RegistryAccessChecker>();
        services.AddHttpClient("RegistryCheck", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(5);
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-RegistryCheck");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

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

        // Version Check Service (v0.16)
        services.AddMemoryCache();
        services.AddSingleton<IVersionCheckService, VersionCheckService>();
        services.AddHttpClient("GitHub");

        // Domain Services
        services.AddScoped<SystemAdminRegistrationService>();
        services.AddScoped<OrganizationProvisioningService>();
        services.AddScoped<AuthenticationService>();

        return services;
    }
}
