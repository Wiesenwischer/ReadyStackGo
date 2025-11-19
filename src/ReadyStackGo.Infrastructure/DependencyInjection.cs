using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Auth;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Application.Stacks;
using ReadyStackGo.Infrastructure.Auth;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Manifests;
using ReadyStackGo.Infrastructure.Stacks;
using ReadyStackGo.Infrastructure.Tls;

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

        // Deployment services
        services.AddSingleton<IDeploymentEngine, DeploymentEngine>();

        // Docker services
        services.AddSingleton<IDockerService, DockerService>();

        // Stack services
        services.AddSingleton<IStackService, StackService>();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IAuthService, AuthService>();

        return services;
    }
}
