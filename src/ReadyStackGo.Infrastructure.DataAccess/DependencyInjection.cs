using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using ReadyStackGo.Domain.Deployment.Registries;
using ReadyStackGo.Domain.IdentityAccess.ApiKeys;
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
        services.AddScoped<IEnvironmentVariableRepository, EnvironmentVariableRepository>();
        services.AddScoped<IDeploymentRepository, DeploymentRepository>();
        services.AddScoped<IHealthSnapshotRepository, HealthSnapshotRepository>();
        services.AddScoped<IRegistryRepository, RegistryRepository>();
        services.AddScoped<IStackSourceRepository, StackSourceRepository>();
        services.AddScoped<IApiKeyRepository, ApiKeyRepository>();
        services.AddScoped<IProductDeploymentRepository, ProductDeploymentRepository>();

        return services;
    }

    /// <summary>
    /// Applies pending EF Core migrations to the database.
    /// Handles three cases:
    ///   1. Fresh database → all migrations run, schema created from scratch.
    ///   2. Legacy database from before migrations were introduced → InitialCreate
    ///      is retroactively marked as applied (baseline), subsequent migrations run.
    ///   3. Existing migrated database → only new pending migrations run.
    /// </summary>
    public static void MigrateDatabase(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();

        // Case 2: Detect legacy database (tables exist from EnsureCreated but no
        // __EFMigrationsHistory). Insert the InitialCreate migration entry so that
        // EF Core treats the existing schema as the baseline.
        if (IsLegacyDatabase(context))
        {
            BaselineLegacyDatabase(context);
        }

        // Case 1 + 3: Apply all pending migrations (or create schema if fresh).
        context.Database.Migrate();
    }

    /// <summary>
    /// Checks if this is a legacy database: tables exist (e.g., StackSources) but
    /// EF Core's __EFMigrationsHistory table does not.
    /// </summary>
    private static bool IsLegacyDatabase(ReadyStackGoDbContext context)
    {
        // Cannot connect at all → fresh database, not legacy
        if (!context.Database.CanConnect())
            return false;

        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) connection.Open();

        try
        {
            using var cmd = connection.CreateCommand();
            // SQLite: check sqlite_master for table presence
            cmd.CommandText =
                "SELECT " +
                "  (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='__EFMigrationsHistory') AS HasHistory, " +
                "  (SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='StackSources') AS HasStackSources";

            using var reader = cmd.ExecuteReader();
            if (!reader.Read()) return false;

            var hasHistory = reader.GetInt32(0) > 0;
            var hasStackSources = reader.GetInt32(1) > 0;

            // Legacy: tables exist but no migration history
            return !hasHistory && hasStackSources;
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    /// <summary>
    /// Creates __EFMigrationsHistory and inserts InitialCreate as already applied,
    /// so that Migrate() only runs subsequent migrations (AddOciRegistrySource, etc.)
    /// against the existing legacy schema.
    /// </summary>
    private static void BaselineLegacyDatabase(ReadyStackGoDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) connection.Open();

        try
        {
            using var tx = connection.BeginTransaction();

            using (var create = connection.CreateCommand())
            {
                create.Transaction = tx;
                create.CommandText =
                    "CREATE TABLE IF NOT EXISTS \"__EFMigrationsHistory\" (" +
                    "  \"MigrationId\" TEXT NOT NULL CONSTRAINT \"PK___EFMigrationsHistory\" PRIMARY KEY, " +
                    "  \"ProductVersion\" TEXT NOT NULL" +
                    ");";
                create.ExecuteNonQuery();
            }

            using (var insert = connection.CreateCommand())
            {
                insert.Transaction = tx;
                insert.CommandText =
                    "INSERT INTO \"__EFMigrationsHistory\" (\"MigrationId\", \"ProductVersion\") " +
                    "VALUES ('20260413183518_InitialCreate', '9.0.0');";
                insert.ExecuteNonQuery();
            }

            tx.Commit();
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }
}
