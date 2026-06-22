using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Impl;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Infrastructure.Caching;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.DataAccess;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Parsing;
using ReadyStackGo.Infrastructure.Security;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Services.Deployment;
using ReadyStackGo.Infrastructure.Services.Deployment.Precheck;
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
        services.AddSingleton<IOnboardingStateService, OnboardingStateService>();
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
        services.AddSingleton<LocalDirectoryProductSourceProvider>();
        services.AddSingleton<IProductSourceProvider>(sp => sp.GetRequiredService<LocalDirectoryProductSourceProvider>());
        services.AddSingleton<IProductSourceProvider, GitRepositoryProductSourceProvider>();
        services.AddSingleton<OciRegistryClient>();
        services.AddSingleton<IProductSourceProvider, OciRegistryProductSourceProvider>();
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

        // OCI Registry Client for stack bundles (v0.58)
        services.AddHttpClient("OciRegistry", client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-OciClient");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        // Deployment Precheck Rules (v0.59)
        services.AddScoped<IDeploymentPrecheckRule, ImageAvailabilityRule>();
        services.AddScoped<IDeploymentPrecheckRule, PortConflictRule>();
        services.AddScoped<IDeploymentPrecheckRule, NetworkAvailabilityRule>();
        services.AddScoped<IDeploymentPrecheckRule, VolumeStatusRule>();

        // Health Monitoring (v0.11)
        services.AddScoped<IHealthMonitoringService, HealthMonitoringService>();
        services.AddScoped<IHealthCollectorService, HealthCollectorService>();
        services.AddSingleton<IHealthChangeTracker, HealthChangeTracker>();

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

        // Maintenance Setter (mirror of the observer — propagates RSGO-initiated transitions)
        services.AddSingleton<IMaintenanceSetterFactory, MaintenanceSetterFactory>();
        services.AddScoped<IMaintenanceSetterService, Application.Services.Impl.MaintenanceSetterService>();

        // Managed Maintenance Edge-Proxy (opt-in per manifest; dormant otherwise)
        services.AddSingleton<Application.Services.Edge.IEdgeProvisioner, Services.Edge.EdgeProvisioner>();
        services.AddSingleton<Application.Services.Edge.ICaddyAdminClient, Services.Edge.CaddyAdminClient>();
        services.AddSingleton<Application.Services.Edge.IEdgeCertificateProvider, Services.Edge.EdgeCertificateProvider>();
        services.AddSingleton<Application.Services.Edge.IEdgeBundleReader, Services.Edge.EdgeBundleReader>();
        services.AddSingleton<Application.Services.Edge.IEdgeConfigCache, Application.Services.Edge.EdgeConfigCache>();
        services.AddScoped<Application.Services.Edge.IEdgeReconciler, Application.Services.Impl.EdgeReconciler>();
        services.AddScoped<Application.Services.Edge.ISniRouterReconciler, Application.Services.Impl.SniRouterReconciler>();

        // HTTP client for the Caddy admin API
        services.AddHttpClient(Services.Edge.CaddyAdminClient.HttpClientName, client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-Edge");
            client.Timeout = TimeSpan.FromSeconds(10);
        });

        // HTTP client for the webhook setter
        services.AddHttpClient("MaintenanceSetter", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-MaintenanceSetter");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });

        // Health check infrastructure services
        services.AddHttpClient<IHttpHealthChecker, HttpHealthChecker>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-HealthChecker");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        services.AddSingleton<ITcpHealthChecker, TcpHealthChecker>();

        // Health check strategies (resolved by type via factory)
        services.AddSingleton<IHealthCheckStrategy, DockerHealthCheckStrategy>();
        services.AddScoped<IHealthCheckStrategy, HttpHealthCheckStrategy>();
        services.AddSingleton<IHealthCheckStrategy, TcpHealthCheckStrategy>();
        // Factory is Scoped because it consumes IEnumerable<IHealthCheckStrategy>,
        // and HttpHealthCheckStrategy is Scoped (it wraps the typed HttpClient
        // IHttpHealthChecker, which AddHttpClient registers as Transient and which
        // therefore must not be captured by a Singleton). All current consumers of
        // IHealthCheckStrategyFactory (HealthMonitoringService, HealthCollectorService)
        // are themselves Scoped, so no behavior changes.
        services.AddScoped<IHealthCheckStrategyFactory, HealthCheckStrategyFactory>();

        // PRTG HTTP API client (Variant 3) — separate verify/no-verify-TLS clients
        // so customers with self-signed PRTG certificates can opt out of cert
        // validation per-connection (set on the PrtgConnection aggregate).
        services.AddHttpClient("PrtgApiVerifyTls", c =>
        {
            c.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-PrtgClient");
        });
        services.AddHttpClient("PrtgApiNoVerifyTls", c =>
        {
            c.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-PrtgClient");
        })
        .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
        {
            ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
        });
        services.AddSingleton<ReadyStackGo.Application.Services.IPrtgApiClient,
                              Services.Prtg.PrtgApiClient>();

        // SSH Tunnel services (v0.49)
        services.AddSingleton<ICredentialEncryptionService, CredentialEncryptionService>();
        services.AddSingleton<Docker.ISshTunnelManager, Docker.SshTunnelManager>();
        services.AddSingleton<ISshConnectionTester, Docker.SshConnectionTester>();

        // Version Check Service (v0.16)
        services.AddMemoryCache();
        services.AddSingleton<IVersionCheckService, VersionCheckService>();
        services.AddHttpClient("GitHub");

        // Notification Service (in-memory, singleton)
        services.AddSingleton<INotificationService, InMemoryNotificationService>();

        // Email (SMTP via MailKit) and SMTP settings store
        services.AddSingleton<Application.Services.Email.ISmtpSettingsService, Services.Email.SmtpSettingsService>();
        services.AddSingleton<Application.Services.Email.IEmailService, Services.Email.SmtpEmailService>();

        // OIDC settings store (the OIDC flow service itself is registered in AddSecurity)
        services.AddSingleton<Application.Services.Oidc.IOidcSettingsService, Services.Oidc.OidcSettingsService>();

        // Domain Services
        services.AddScoped<SystemAdminRegistrationService>();
        services.AddScoped<OrganizationProvisioningService>();
        services.AddScoped<AuthenticationService>();

        return services;
    }
}
