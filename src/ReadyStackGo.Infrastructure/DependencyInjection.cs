using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Roles;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Infrastructure.Authentication;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Docker;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Services;
using ReadyStackGo.Infrastructure.Manifests;
using ReadyStackGo.Infrastructure.DataAccess;
using ReadyStackGo.Infrastructure.DataAccess.Repositories;
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
        services.AddSingleton<ISystemConfigService, SystemConfigService>();
        services.AddSingleton<IWizardTimeoutService, WizardTimeoutService>();

        // TLS services
        services.AddSingleton<ITlsService, TlsService>();

        // Manifest services
        services.AddSingleton<IManifestProvider, ManifestProvider>();
        services.AddSingleton<IDockerComposeParser, DockerComposeParser>();
        services.AddSingleton<IRsgoManifestParser, RsgoManifestParser>();

        // Deployment services
        // v0.6: DeploymentEngine is Scoped because it depends on Scoped repositories
        services.AddScoped<IDeploymentEngine, DeploymentEngine>();
        services.AddScoped<IDeploymentService, DeploymentService>();

        // Docker services
        // v0.6: DockerService is Scoped because it depends on Scoped IEnvironmentRepository
        services.AddScoped<IDockerService, DockerService>();

        // Stack source services
        services.AddSingleton<IStackCache, InMemoryStackCache>();
        services.AddSingleton<IStackSourceProvider, LocalDirectoryStackSourceProvider>();
        services.AddSingleton<IStackSourceService, StackSourceService>();

        // Auth services
        services.Configure<JwtSettings>(configuration.GetSection("Jwt"));
        services.AddSingleton<ITokenService, TokenService>();
        services.AddSingleton<IRbacService, RbacService>();

        // Environment services (v0.6 SQLite)
        services.AddScoped<IEnvironmentService, EnvironmentService>();

        // v0.6: SQLite persistence
        // Use DataPath for database, separate from config files
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
