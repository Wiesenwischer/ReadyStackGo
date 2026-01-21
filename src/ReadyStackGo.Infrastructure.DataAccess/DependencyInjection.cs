using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;

namespace ReadyStackGo.Infrastructure.DataAccess;

public static class DependencyInjection
{
    public static IServiceCollection AddDataAccess(this IServiceCollection services, IConfiguration configuration)
    {
        // SQLite persistence
        var connectionString = configuration.GetConnectionString("ReadyStackGo");
        if (string.IsNullOrEmpty(connectionString))
        {
            var dataPath = configuration["DataPath"] ?? "data";
            var dbPath = Path.Combine(dataPath, "readystackgo.db");
            connectionString = $"Data Source={dbPath}";
        }

        services.AddDbContext<ReadyStackGoDbContext>(options =>
            options.UseSqlite(connectionString));

        // Repositories
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IRoleRepository, RoleRepository>();
        services.AddScoped<IEnvironmentRepository, EnvironmentRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        services.AddScoped<IHealthSnapshotRepository, HealthSnapshotRepository>();
        services.AddScoped<IRegistryRepository, RegistryRepository>();
        services.AddScoped<IStackSourceRepository, StackSourceRepository>();

        return services;
    }

    /// <summary>
    /// Ensures the database is created.
    /// </summary>
    public static void EnsureDatabaseCreated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();
        context.Database.EnsureCreated();
    }
}
