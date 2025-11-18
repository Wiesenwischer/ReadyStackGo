using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Auth;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Infrastructure.Auth;
using ReadyStackGo.Infrastructure.Docker;

namespace ReadyStackGo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        // Docker services
        services.AddSingleton<IDockerService, DockerService>();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IAuthService, AuthService>();

        return services;
    }
}
