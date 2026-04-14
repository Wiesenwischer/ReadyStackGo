using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Infrastructure.DataAccess;

namespace ReadyStackGo.UnitTests.Infrastructure.DataAccess;

/// <summary>
/// Tests for the EF Core migration startup logic, especially the legacy-database
/// baseline detection that allows upgrading databases created via EnsureCreated()
/// before migrations were introduced.
/// </summary>
public class MigrationBaselineTests
{
    [Fact]
    public void MigrateDatabase_FreshDatabase_CreatesSchemaAndAllMigrationsApplied()
    {
        // Fresh in-memory database, no tables exist.
        var services = BuildServices(out var connection);
        try
        {
            services.MigrateDatabase();

            // Verify StackSources table exists with OCI columns (from AddOciRegistrySource)
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();

            ColumnExists(context, "StackSources", "RegistryUrl").Should().BeTrue();
            ColumnExists(context, "StackSources", "Repository").Should().BeTrue();
            MigrationHistoryCount(context).Should().BeGreaterThanOrEqualTo(2);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void MigrateDatabase_LegacyDatabase_AppliesOciMigrationWithoutReCreatingBaselineTables()
    {
        // Simulate a legacy database: create schema manually matching the
        // InitialCreate baseline (without OCI columns) — no __EFMigrationsHistory.
        var services = BuildServices(out var connection);
        try
        {
            CreateLegacyStackSourcesTable(connection);

            // Verify pre-conditions: table exists without OCI, no migration history
            TableExists(connection, "StackSources").Should().BeTrue();
            TableExists(connection, "__EFMigrationsHistory").Should().BeFalse();
            ColumnExistsRaw(connection, "StackSources", "RegistryUrl").Should().BeFalse();

            // Act: run migration startup logic
            services.MigrateDatabase();

            // Assert: OCI columns added, history table created with InitialCreate + AddOciRegistrySource
            TableExists(connection, "__EFMigrationsHistory").Should().BeTrue();
            ColumnExistsRaw(connection, "StackSources", "RegistryUrl").Should().BeTrue();
            ColumnExistsRaw(connection, "StackSources", "Repository").Should().BeTrue();
            ColumnExistsRaw(connection, "StackSources", "RegistryUsername").Should().BeTrue();
            ColumnExistsRaw(connection, "StackSources", "RegistryPassword").Should().BeTrue();
            ColumnExistsRaw(connection, "StackSources", "TagPattern").Should().BeTrue();
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void MigrateDatabase_LegacyDatabase_PreservesExistingData()
    {
        // Data in legacy DB must survive the baseline + migration run
        var services = BuildServices(out var connection);
        try
        {
            CreateLegacyStackSourcesTable(connection);
            InsertLegacyStackSource(connection, id: "legacy-source", name: "Legacy Source");

            services.MigrateDatabase();

            // Verify the row is still there and readable via EF Core
            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();
            var source = context.StackSources.FirstOrDefault();

            source.Should().NotBeNull();
            source!.Name.Should().Be("Legacy Source");
            source.RegistryUrl.Should().BeNull(); // New column, no value
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void MigrateDatabase_RunTwice_IsIdempotent()
    {
        // Running migrations twice must not fail or duplicate anything
        var services = BuildServices(out var connection);
        try
        {
            services.MigrateDatabase();
            services.MigrateDatabase(); // Second call should be a no-op

            using var scope = services.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();
            MigrationHistoryCount(context).Should().BeGreaterThanOrEqualTo(2);
        }
        finally
        {
            connection.Dispose();
        }
    }

    // ---------- helpers ----------

    private static ServiceProvider BuildServices(out SqliteConnection connection)
    {
        // Use a shared, open in-memory SQLite connection so the schema persists
        // across scopes (DbContext instances).
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        var services = new ServiceCollection();
        var sharedConnection = connection;
        services.AddDbContext<ReadyStackGoDbContext>(options => options.UseSqlite(sharedConnection));
        return services.BuildServiceProvider();
    }

    private static void CreateLegacyStackSourcesTable(SqliteConnection connection)
    {
        // Matches the InitialCreate baseline schema — includes the Version column
        // inherited from AggregateRoot for optimistic concurrency, but no OCI columns.
        WithOpenConnection(connection, () =>
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE "StackSources" (
                    "Id" TEXT NOT NULL PRIMARY KEY,
                    "Name" TEXT NOT NULL,
                    "Type" TEXT NOT NULL,
                    "Enabled" INTEGER NOT NULL,
                    "LastSyncedAt" TEXT NULL,
                    "CreatedAt" TEXT NOT NULL,
                    "Path" TEXT NULL,
                    "FilePattern" TEXT NULL,
                    "GitUrl" TEXT NULL,
                    "GitBranch" TEXT NULL,
                    "GitUsername" TEXT NULL,
                    "GitPassword" TEXT NULL,
                    "GitSslVerify" INTEGER NOT NULL DEFAULT 1,
                    "Version" INTEGER NOT NULL DEFAULT 0
                );
                """;
            cmd.ExecuteNonQuery();
            return 0;
        });
    }

    private static void InsertLegacyStackSource(SqliteConnection connection, string id, string name)
    {
        WithOpenConnection(connection, () =>
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = """
                INSERT INTO "StackSources"
                    ("Id", "Name", "Type", "Enabled", "CreatedAt", "Path", "FilePattern", "Version")
                VALUES
                    (@id, @name, 'LocalDirectory', 1, @now, '/stacks', '*.yml;*.yaml', 0);
                """;
            cmd.Parameters.AddWithValue("@id", id);
            cmd.Parameters.AddWithValue("@name", name);
            cmd.Parameters.AddWithValue("@now", DateTime.UtcNow.ToString("O"));
            cmd.ExecuteNonQuery();
            return 0;
        });
    }

    private static bool TableExists(SqliteConnection connection, string tableName)
    {
        return WithOpenConnection(connection, () =>
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name=@name";
            cmd.Parameters.AddWithValue("@name", tableName);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        });
    }

    private static bool ColumnExistsRaw(SqliteConnection connection, string table, string column)
    {
        return WithOpenConnection(connection, () =>
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info(\"{table}\");";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1).Equals(column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        });
    }

    private static T WithOpenConnection<T>(SqliteConnection connection, Func<T> action)
    {
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) connection.Open();
        try
        {
            return action();
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    private static bool ColumnExists(ReadyStackGoDbContext context, string table, string column)
    {
        var connection = (SqliteConnection)context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) connection.Open();
        try
        {
            return ColumnExistsRaw(connection, table, column);
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }

    private static int MigrationHistoryCount(ReadyStackGoDbContext context)
    {
        var connection = context.Database.GetDbConnection();
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed) connection.Open();
        try
        {
            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM \"__EFMigrationsHistory\"";
            return Convert.ToInt32(cmd.ExecuteScalar());
        }
        finally
        {
            if (wasClosed) connection.Close();
        }
    }
}
