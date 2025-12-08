using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service that periodically collects health data from all deployments.
/// </summary>
public class HealthCollectorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCollectorBackgroundService> _logger;
    private readonly HealthCollectorOptions _options;

    public HealthCollectorBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<HealthCollectorOptions> options,
        ILogger<HealthCollectorBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Health Collector Background Service starting. Interval: {Interval}s",
            _options.CollectionIntervalSeconds);

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectHealthAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Expected when stopping
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during health collection cycle");
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(_options.CollectionIntervalSeconds),
                    stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Health Collector Background Service stopped");
    }

    private async Task CollectHealthAsync(CancellationToken stoppingToken)
    {
        _logger.LogDebug("Starting health collection cycle");

        // Create a scope for the DI services
        using var scope = _serviceProvider.CreateScope();
        var healthCollector = scope.ServiceProvider.GetRequiredService<IHealthCollectorService>();

        await healthCollector.CollectAllHealthAsync(stoppingToken);

        _logger.LogDebug("Health collection cycle completed");
    }
}

/// <summary>
/// Configuration options for the health collector background service.
/// </summary>
public class HealthCollectorOptions
{
    public const string SectionName = "HealthCollector";

    /// <summary>
    /// Interval between health collection cycles in seconds.
    /// Default: 30 seconds.
    /// </summary>
    public int CollectionIntervalSeconds { get; set; } = 30;

    /// <summary>
    /// Initial delay before first collection in seconds.
    /// Allows the application to fully start up before collecting health.
    /// Default: 10 seconds.
    /// </summary>
    public int InitialDelaySeconds { get; set; } = 10;

    /// <summary>
    /// Whether the health collector is enabled.
    /// Default: true.
    /// </summary>
    public bool Enabled { get; set; } = true;
}
