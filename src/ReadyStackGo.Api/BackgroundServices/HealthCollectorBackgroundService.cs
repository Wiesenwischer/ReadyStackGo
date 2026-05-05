using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Health;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service that periodically collects health data from all deployments.
/// </summary>
public class HealthCollectorBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<HealthCollectorBackgroundService> _logger;
    private readonly HealthCollectorOptions _options;
    private DateTime _lastCleanup = DateTime.MinValue;

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
            "Health Collector Background Service starting. Interval: {Interval}s, Retention: {Retention} days",
            _options.CollectionIntervalSeconds, _options.RetentionDays);

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromSeconds(_options.InitialDelaySeconds), stoppingToken);

        // Ensure this container is connected to the management network
        await EnsureManagementNetworkAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CollectHealthAsync(stoppingToken);
                CleanupOldSnapshots();
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

    /// <summary>
    /// Ensures this container is connected to the rsgo-net management network.
    /// Repairs the connection if it was lost (e.g., after a self-update that didn't preserve it).
    /// </summary>
    private async Task EnsureManagementNetworkAsync()
    {
        const string managementNetwork = "rsgo-net";

        // Only relevant when running inside Docker
        var hostname = Environment.GetEnvironmentVariable("HOSTNAME");
        if (string.IsNullOrEmpty(hostname))
        {
            _logger.LogDebug("Not running in Docker, skipping management network check");
            return;
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dockerService = scope.ServiceProvider.GetRequiredService<ISelfUpdateService>();

            // ISelfUpdateService has access to the local Docker client
            // Use it to check and connect our container to rsgo-net
            var docker = GetLocalDockerClient();

            // Check if rsgo-net exists
            try
            {
                await docker.Networks.InspectNetworkAsync(managementNetwork);
            }
            catch (Docker.DotNet.DockerNetworkNotFoundException)
            {
                _logger.LogInformation("Creating management network {Network}", managementNetwork);
                await docker.Networks.CreateNetworkAsync(new Docker.DotNet.Models.NetworksCreateParameters
                {
                    Name = managementNetwork,
                    Driver = "bridge"
                });
            }

            // Check if this container is already on the network
            var inspection = await docker.Containers.InspectContainerAsync(hostname);
            if (inspection.NetworkSettings?.Networks?.ContainsKey(managementNetwork) == true)
            {
                _logger.LogDebug("Container is already connected to {Network}", managementNetwork);
                docker.Dispose();
                return;
            }

            // Connect this container to rsgo-net
            await docker.Networks.ConnectNetworkAsync(managementNetwork, new Docker.DotNet.Models.NetworkConnectParameters
            {
                Container = hostname
            });

            _logger.LogWarning(
                "Container was not on {Network} — connected automatically. Health checks should now work correctly",
                managementNetwork);

            docker.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to ensure management network connectivity. Health checks may not work for HTTP-checked services");
        }
    }

    private static Docker.DotNet.DockerClient GetLocalDockerClient()
    {
        var socketUri = OperatingSystem.IsWindows()
            ? new Uri("npipe://./pipe/docker_engine")
            : new Uri("unix:///var/run/docker.sock");
        return new Docker.DotNet.DockerClientConfiguration(socketUri).CreateClient();
    }

    private void CleanupOldSnapshots()
    {
        // Run cleanup once per hour
        if (DateTime.UtcNow - _lastCleanup < TimeSpan.FromHours(1))
            return;

        _lastCleanup = DateTime.UtcNow;

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IHealthSnapshotRepository>();

            var deleted = repository.RemoveOlderThan(TimeSpan.FromDays(_options.RetentionDays));
            if (deleted > 0)
            {
                _logger.LogInformation(
                    "Cleaned up {Count} health snapshots older than {Days} days",
                    deleted, _options.RetentionDays);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup old health snapshots");
        }
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

    /// <summary>
    /// Number of days to retain health snapshots.
    /// Snapshots older than this are automatically deleted.
    /// Default: 7 days. With a 30s collection interval this still produces ~20k snapshots
    /// per deployment; longer retention is unnecessary because the UI history view
    /// only requests the latest 50 snapshots per deployment.
    /// </summary>
    public int RetentionDays { get; set; } = 7;
}
