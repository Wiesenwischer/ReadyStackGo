using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Containers;
using ReadyStackGo.Infrastructure.Docker;

namespace ReadyStackGo.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IDockerService, DockerService>();

        return services;
    }
}
