using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Api;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Persistence;

namespace ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that provides isolated test environments.
/// Each test class can get a fresh ConfigStore state by using this factory.
/// Uses in-memory SQLite database for test isolation.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testId;
    private readonly string _testConfigPath;
    private SqliteConnection? _connection;

    public CustomWebApplicationFactory()
    {
        _testId = Guid.NewGuid().ToString("N");
        _testConfigPath = Path.Combine(
            Path.GetTempPath(),
            "ReadyStackGo.Tests",
            _testId
        );

        // Ensure directory exists
        Directory.CreateDirectory(_testConfigPath);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((context, config) =>
        {
            // Add test-specific configuration
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConfigPath"] = _testConfigPath,
                ["ManifestsPath"] = Path.Combine(_testConfigPath, "manifests")
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing ConfigStore registration
            var configDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConfigStore));
            if (configDescriptor != null)
            {
                services.Remove(configDescriptor);
            }

            // Add a test-specific ConfigStore that uses isolated storage
            services.AddSingleton<IConfigStore>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                return new ConfigStore(configuration);
            });

            // Remove the existing DbContext registration
            var dbContextDescriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<ReadyStackGoDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            // Create and open SQLite in-memory connection
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            // Add in-memory SQLite DbContext
            services.AddDbContext<ReadyStackGoDbContext>(options =>
            {
                options.UseSqlite(_connection);
            });

            // Build service provider and ensure database is created
            var sp = services.BuildServiceProvider();
            using var scope = sp.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ReadyStackGoDbContext>();
            db.Database.EnsureCreated();
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up SQLite connection
            _connection?.Close();
            _connection?.Dispose();

            // Clean up test directory
            if (Directory.Exists(_testConfigPath))
            {
                try
                {
                    Directory.Delete(_testConfigPath, true);
                }
                catch
                {
                    // Ignore cleanup errors
                }
            }
        }

        base.Dispose(disposing);
    }
}
