using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ReadyStackGo.UnitTests.Configuration;

/// <summary>
/// Guards the shipped log levels. RSGO polls its SQLite database from several background services;
/// with EF Core at Information every one of those queries is written to the container log, which
/// grew to more than 50 GB on a long-running host and filled its disk.
/// </summary>
public class LoggingConfigurationTests
{
    private const string EfCoreCommandCategory = "Microsoft.EntityFrameworkCore.Database.Command";

    [Fact]
    public void ShippedAppSettings_DoNotLogEfCoreSqlCommands()
    {
        // Arrange
        var logger = CreateLoggerFromShippedAppSettings(EfCoreCommandCategory);

        // Assert
        logger.IsEnabled(LogLevel.Information).Should().BeFalse(
            "every polled SQL command would otherwise be written to the container log");
        logger.IsEnabled(LogLevel.Debug).Should().BeFalse();
    }

    [Fact]
    public void ShippedAppSettings_StillLogEfCoreWarnings()
    {
        var logger = CreateLoggerFromShippedAppSettings("Microsoft.EntityFrameworkCore.Query");

        logger.IsEnabled(LogLevel.Warning).Should().BeTrue();
        logger.IsEnabled(LogLevel.Error).Should().BeTrue();
    }

    [Fact]
    public void ShippedAppSettings_StillLogApplicationInformation()
    {
        var logger = CreateLoggerFromShippedAppSettings("ReadyStackGo.Infrastructure.Docker.SelfUpdateService");

        logger.IsEnabled(LogLevel.Information).Should().BeTrue(
            "the self-update flow and deployments are diagnosed from Information messages");
    }

    private static ILogger CreateLoggerFromShippedAppSettings(string category)
    {
        // appsettings.json of ReadyStackGo.Api is copied next to the test assembly via the project reference.
        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(builder =>
        {
            builder.AddConfiguration(configuration.GetSection("Logging"));
            builder.AddProvider(new AlwaysEnabledLoggerProvider());
        });

        var factory = services.BuildServiceProvider().GetRequiredService<ILoggerFactory>();
        return factory.CreateLogger(category);
    }

    /// <summary>
    /// Provider whose loggers accept every level, so that only the configured filters decide.
    /// </summary>
    private sealed class AlwaysEnabledLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new AlwaysEnabledLogger();

        public void Dispose()
        {
        }

        private sealed class AlwaysEnabledLogger : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
            }
        }
    }
}
