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

        // Maintenance Observers (v0.11) — the HTTP observer client is registered in
        // AddInternalHttpClients (it targets a product endpoint and must bypass the proxy).
        services.AddSingleton<IMaintenanceObserverFactory, MaintenanceObserverFactory>();
        services.AddScoped<IMaintenanceObserverService, MaintenanceObserverService>();

        // Maintenance Setter (mirror of the observer — propagates RSGO-initiated transitions)
        services.AddSingleton<IMaintenanceSetterFactory, MaintenanceSetterFactory>();
        services.AddScoped<IMaintenanceSetterService, Application.Services.Impl.MaintenanceSetterService>();

        // Managed Maintenance Edge-Proxy (opt-in per manifest; dormant otherwise)
        // Scoped: the provisioner depends on the scoped IDockerService — a singleton would be a
        // captive dependency that ASP.NET Core scope validation rejects at startup.
        services.AddScoped<Application.Services.Edge.IEdgeProvisioner, Services.Edge.EdgeProvisioner>();
        services.AddSingleton<Application.Services.Edge.ICaddyAdminClient, Services.Edge.CaddyAdminClient>();
        services.AddSingleton<Application.Services.Edge.IEdgeCertificateProvider, Services.Edge.EdgeCertificateProvider>();
        services.AddSingleton<Application.Services.Edge.IEdgeBundleReader, Services.Edge.EdgeBundleReader>();
        services.AddSingleton<Application.Services.Edge.IEdgeConfigCache, Application.Services.Edge.EdgeConfigCache>();
        services.AddScoped<Application.Services.Edge.IEdgeReconciler, Application.Services.Impl.EdgeReconciler>();
        services.AddScoped<Application.Services.Edge.ISniRouterReconciler, Application.Services.Impl.SniRouterReconciler>();

        // Internal/LAN HTTP clients — edge admin API, HTTP maintenance observer/setter, HTTP
        // health checks and PRTG. All bypass any forward proxy (HTTP_PROXY/HTTPS_PROXY); see
        // AddInternalHttpClients (issue #446).
        services.AddInternalHttpClients();

        // Health check infrastructure services (the HTTP checker client is registered in
        // AddInternalHttpClients — it targets product containers and must bypass the proxy).
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

        // PRTG HTTP API client (Variant 3): the verify / no-verify-TLS HttpClients are registered
        // in AddInternalHttpClients — PRTG lives on the customer LAN and must bypass the proxy.
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

    /// <summary>
    /// Registers every HTTP client whose target is reachable directly — a product container on
    /// the internal Docker network (edge admin API, HTTP maintenance observer/setter, HTTP health
    /// checks) or the PRTG server on the customer LAN. All of these must bypass a forward proxy
    /// (<c>HTTP_PROXY</c>/<c>HTTPS_PROXY</c>): routing an internal/LAN call through an internet
    /// proxy makes it fail — issue #446, where the edge admin <c>/load</c> POST was hijacked by
    /// the proxy, failed silently, and stranded the edge on its bootstrap holding page; the same
    /// failure class hits health checks and the maintenance observer/setter. Clients that talk to
    /// the public internet (registries, GitHub, Cloudflare) keep the default proxy behaviour and
    /// are registered separately.
    /// </summary>
    public static IServiceCollection AddInternalHttpClients(this IServiceCollection services)
    {
        // Caddy admin API (edge container).
        services.AddEdgeAdminHttpClient();

        // HTTP maintenance observer — reads the product's flag from a product endpoint.
        services.AddHttpClient("MaintenanceObserver", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-MaintenanceObserver");
        })
        .ConfigurePrimaryHttpMessageHandler(() => InternalHttpClientHandler(acceptAnyServerCert: true));

        // Webhook maintenance setter — pushes the transition to a product endpoint.
        services.AddHttpClient("MaintenanceSetter", client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-MaintenanceSetter");
        })
        .ConfigurePrimaryHttpMessageHandler(() => InternalHttpClientHandler(acceptAnyServerCert: true));

        // HTTP health checks against the deployed product containers.
        services.AddHttpClient<IHttpHealthChecker, HttpHealthChecker>(client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-HealthChecker");
        })
        .ConfigurePrimaryHttpMessageHandler(() => InternalHttpClientHandler(acceptAnyServerCert: true));

        // PRTG monitoring API on the customer LAN — separate verify / no-verify-TLS clients so
        // customers with self-signed PRTG certificates can opt out of cert validation per-connection.
        services.AddHttpClient("PrtgApiVerifyTls", c =>
        {
            c.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-PrtgClient");
        })
        .ConfigurePrimaryHttpMessageHandler(() => InternalHttpClientHandler(acceptAnyServerCert: false));
        services.AddHttpClient("PrtgApiNoVerifyTls", c =>
        {
            c.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-PrtgClient");
        })
        .ConfigurePrimaryHttpMessageHandler(() => InternalHttpClientHandler(acceptAnyServerCert: true));

        return services;
    }

    /// <summary>
    /// Registers the named <see cref="System.Net.Http.HttpClient"/> used to talk to a product
    /// edge's Caddy admin API. Container-internal — must bypass any forward proxy (issue #446).
    /// </summary>
    public static IServiceCollection AddEdgeAdminHttpClient(this IServiceCollection services)
    {
        services.AddHttpClient(Services.Edge.CaddyAdminClient.HttpClientName, client =>
        {
            client.DefaultRequestHeaders.Add("User-Agent", "ReadyStackGo-Edge");
            client.Timeout = TimeSpan.FromSeconds(10);
        })
        .ConfigurePrimaryHttpMessageHandler(() => InternalHttpClientHandler(acceptAnyServerCert: false));
        return services;
    }

    /// <summary>
    /// Primary handler for HTTP clients whose target is reachable directly (internal Docker
    /// network or customer LAN). Sets <c>UseProxy = false</c> so a forward proxy configured via
    /// <c>HTTP_PROXY</c>/<c>HTTPS_PROXY</c> can never hijack the call (issue #446), and optionally
    /// accepts any server certificate (for self-signed product/PRTG endpoints).
    /// </summary>
    private static HttpClientHandler InternalHttpClientHandler(bool acceptAnyServerCert) => new()
    {
        UseProxy = false,
        ServerCertificateCustomValidationCallback = acceptAnyServerCert
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null
    };
}
