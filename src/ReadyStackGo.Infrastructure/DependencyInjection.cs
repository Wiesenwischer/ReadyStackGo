using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Auth;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Deployments;
using ReadyStackGo.Application.Environments;
using ReadyStackGo.Application.Manifests;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Application.Wizard;
using ReadyStackGo.Infrastructure.Auth;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Deployments;
using ReadyStackGo.Infrastructure.Environments;
using ReadyStackGo.Infrastructure.Manifests;
using ReadyStackGo.Infrastructure.Stacks;
using ReadyStackGo.Infrastructure.Stacks.Sources;
using ReadyStackGo.Infrastructure.Tls;
using ReadyStackGo.Infrastructure.Wizard;

namespace ReadyStackGo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration services
        services.AddSingleton<IConfigStore, ConfigStore>();

        // TLS services
        services.AddSingleton<ITlsService, TlsService>();

        // Manifest services
        services.AddSingleton<IManifestProvider, ManifestProvider>();
        services.AddSingleton<IDockerComposeParser, DockerComposeParser>();

        // Deployment services
        services.AddSingleton<IDeploymentEngine, DeploymentEngine>();
        services.AddSingleton<IDeploymentService, DeploymentService>();

        // Docker services
        services.AddSingleton<IDockerService, DockerService>();

        // Stack source services
        services.AddSingleton<IStackCache, InMemoryStackCache>();
        services.AddSingleton<IStackSourceProvider, LocalDirectoryStackSourceProvider>();
        services.AddSingleton<IStackSourceService, StackSourceService>();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IAuthService, AuthService>();

        // Wizard services
        services.AddSingleton<IWizardService, WizardService>();

        // Environment services (v0.4)
        services.AddSingleton<IEnvironmentService, EnvironmentService>();

        return services;
    }
}
