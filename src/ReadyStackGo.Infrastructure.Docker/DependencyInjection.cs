using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Docker;

public static class DependencyInjection
{
    public static IServiceCollection AddDocker(this IServiceCollection services)
    {
        // Docker services
        services.AddScoped<IDockerService, DockerService>();

        // Docker Compose parsing
        services.AddSingleton<IDockerComposeParser, DockerComposeParser>();

        // Self-update service (connects directly to local Docker socket)
        services.AddSingleton<ISelfUpdateService, SelfUpdateService>();

        return services;
    }
}
