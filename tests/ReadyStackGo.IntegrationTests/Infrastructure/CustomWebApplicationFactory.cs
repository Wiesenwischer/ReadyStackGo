using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ReadyStackGo.Api;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.IntegrationTests.Infrastructure;

/// <summary>
/// Custom WebApplicationFactory that provides isolated test environments.
/// Each test class can get a fresh ConfigStore state by using this factory.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _testId;
    private readonly string _testConfigPath;

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
                ["ConfigPath"] = _testConfigPath
            });
        });

        builder.ConfigureServices(services =>
        {
            // Remove the existing ConfigStore registration
            var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(IConfigStore));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add a test-specific ConfigStore that uses isolated storage
            services.AddSingleton<IConfigStore>(sp =>
            {
                var configuration = sp.GetRequiredService<IConfiguration>();
                return new ConfigStore(configuration);
            });
        });

        builder.UseEnvironment("Testing");
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
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
