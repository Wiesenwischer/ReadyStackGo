using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Identity.Repositories;
using ReadyStackGo.Domain.Identity.Services;
using ReadyStackGo.Domain.Access.Repositories;
using ReadyStackGo.Domain.StackManagement.Repositories;
using ReadyStackGo.Infrastructure.Auth;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Deployment;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Deployments;
using ReadyStackGo.Infrastructure.Environments;
using ReadyStackGo.Infrastructure.Manifests;
using ReadyStackGo.Infrastructure.Persistence;
using ReadyStackGo.Infrastructure.Persistence.Repositories;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Stacks;
using ReadyStackGo.Infrastructure.Stacks.Sources;
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
        services.AddSingleton<IDockerComposeParser, DockerComposeParser>();

        // Deployment services
        services.AddSingleton<IDeploymentEngine, DeploymentEngine>();
        services.AddScoped<IDeploymentService, DeploymentService>();

        // Docker services
        services.AddSingleton<IDockerService, DockerService>();

        // Stack source services
        services.AddSingleton<IStackCache, InMemoryStackCache>();
        services.AddSingleton<IStackSourceProvider, LocalDirectoryStackSourceProvider>();
        services.AddSingleton<IStackSourceService, StackSourceService>();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();

        // Environment services (v0.6 SQLite)
        services.AddScoped<IEnvironmentService, EnvironmentService>();

        // v0.6: SQLite persistence
        // Use ConfigPath-based path if no explicit connection string configured
        var connectionString = configuration.GetConnectionString("ReadyStackGo");
        if (string.IsNullOrEmpty(connectionString))
        {
            var configPath = configuration["ConfigPath"] ?? "config";
            var dbPath = Path.Combine(configPath, "readystackgo.db");
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

        // Password hashing
        services.AddSingleton<IPasswordHasher, BCryptPasswordHasher>();

        // Domain Services
        services.AddScoped<SystemAdminRegistrationService>();
        services.AddScoped<OrganizationProvisioningService>();
        services.AddScoped<AuthenticationService>();

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
