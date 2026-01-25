namespace ReadyStackGo.Application.Services.Impl;

using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// Service for monitoring health of deployed stacks.
/// Collects container status from Docker and aggregates into health snapshots.
/// Supports both Docker HEALTHCHECK and direct HTTP health endpoint checks.
/// </summary>
public class HealthMonitoringService : IHealthMonitoringService
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IHttpHealthChecker? _httpHealthChecker;
    private readonly ILogger<HealthMonitoringService> _logger;

    public HealthMonitoringService(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IHealthSnapshotRepository healthSnapshotRepository,
        ILogger<HealthMonitoringService> logger,
        IHttpHealthChecker? httpHealthChecker = null)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _healthSnapshotRepository = healthSnapshotRepository;
        _httpHealthChecker = httpHealthChecker;
        _logger = logger;
    }

    public async Task<HealthSnapshot> CaptureHealthSnapshotAsync(
        OrganizationId organizationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        string stackName,
        string? currentVersion = null,
        IReadOnlyDictionary<string, ServiceHealthCheckConfig>? serviceHealthConfigs = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Capturing health snapshot for deployment {DeploymentId}", deploymentId);

        // Get current deployment to determine operation mode
        var deployment = _deploymentRepository.Get(deploymentId);
        var operationMode = DetermineOperationMode(deployment);

        // Collect container health from Docker (with optional HTTP health checks)
        var selfHealth = await CollectSelfHealthAsync(
            environmentId.Value.ToString(),
            stackName,
            serviceHealthConfigs,
            cancellationToken);

        // Create and persist the snapshot
        var snapshot = HealthSnapshot.Capture(
            organizationId,
            environmentId,
            deploymentId,
            stackName,
            operationMode,
            currentVersion ?? deployment?.StackVersion,
            targetVersion: null, // Set during migrations
            bus: null, // TODO: Implement NSB health collection
            infra: null, // TODO: Implement infra health collection
            self: selfHealth);

        _healthSnapshotRepository.Add(snapshot);
        _healthSnapshotRepository.SaveChanges();

        _logger.LogInformation(
            "Captured health snapshot {SnapshotId} for {StackName}: Overall={Overall}, Mode={Mode}",
            snapshot.Id, stackName, snapshot.Overall.Name, snapshot.OperationMode.Name);

        return snapshot;
    }

    public Task<HealthSnapshot?> GetLatestHealthSnapshotAsync(
        DeploymentId deploymentId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = _healthSnapshotRepository.GetLatestForDeployment(deploymentId);
        return Task.FromResult(snapshot);
    }

    public Task<IEnumerable<HealthSnapshot>> GetEnvironmentHealthSummaryAsync(
        EnvironmentId environmentId,
        CancellationToken cancellationToken = default)
    {
        var snapshots = _healthSnapshotRepository.GetLatestForEnvironment(environmentId);
        return Task.FromResult(snapshots);
    }

    public Task<IEnumerable<HealthSnapshot>> GetHealthHistoryAsync(
        DeploymentId deploymentId,
        int limit = 10,
        CancellationToken cancellationToken = default)
    {
        var history = _healthSnapshotRepository.GetHistory(deploymentId, limit);
        return Task.FromResult(history);
    }

    public Task UpdateOperationModeAsync(
        DeploymentId deploymentId,
        OperationMode newMode,
        string? targetVersion = null,
        CancellationToken cancellationToken = default)
    {
        // Operation mode is stored on the Deployment aggregate
        // This is a placeholder for future implementation
        _logger.LogInformation(
            "Operation mode update requested for {DeploymentId}: {NewMode}",
            deploymentId, newMode.Name);

        // TODO: Update deployment's operation mode and capture new snapshot
        return Task.CompletedTask;
    }

    /// <summary>
    /// Collects health status of all services/containers for a stack.
    /// Supports both Docker HEALTHCHECK and HTTP health endpoint checks.
    /// RestartCount is only queried for unhealthy containers to minimize API calls.
    /// </summary>
    private async Task<SelfHealth> CollectSelfHealthAsync(
        string environmentId,
        string stackName,
        IReadOnlyDictionary<string, ServiceHealthCheckConfig>? serviceHealthConfigs,
        CancellationToken cancellationToken)
    {
        try
        {
            // Use fast ListContainersAsync first
            var containers = await _dockerService.ListContainersAsync(environmentId, cancellationToken);

            // Filter containers belonging to this stack (by project label or name prefix)
            var stackContainers = containers
                .Where(c => BelongsToStack(c, stackName))
                .ToList();

            if (!stackContainers.Any())
            {
                _logger.LogWarning("No containers found for stack {StackName}", stackName);
                return SelfHealth.Empty();
            }

            // Map containers to service health, with HTTP health checks if configured
            var serviceHealthTasks = stackContainers.Select(async c =>
            {
                var serviceName = ExtractServiceName(c);
                ServiceHealthCheckConfig? healthConfig = null;
                serviceHealthConfigs?.TryGetValue(serviceName, out healthConfig);

                return await CollectServiceHealthAsync(
                    c,
                    serviceName,
                    healthConfig,
                    environmentId,
                    cancellationToken);
            });

            var serviceHealthList = await Task.WhenAll(serviceHealthTasks);
            return SelfHealth.Create(serviceHealthList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect container health for stack {StackName}", stackName);
            return SelfHealth.Empty();
        }
    }

    /// <summary>
    /// Collects health for a single service/container.
    /// Uses HTTP health check if configured, otherwise falls back to Docker status.
    /// </summary>
    private async Task<ServiceHealth> CollectServiceHealthAsync(
        ContainerDto container,
        string serviceName,
        ServiceHealthCheckConfig? healthConfig,
        string environmentId,
        CancellationToken cancellationToken)
    {
        HealthStatus healthStatus;
        string? reason = null;
        int? restartCount = null;

        // Check if container is running first - if not, HTTP check doesn't make sense
        if (container.State.ToLowerInvariant() != "running")
        {
            healthStatus = DetermineHealthStatusFromDocker(container);
            reason = DetermineHealthReason(container, healthStatus);
        }
        // Use HTTP health check if configured and available
        else if (healthConfig?.IsHttp == true && _httpHealthChecker != null)
        {
            var httpResult = await PerformHttpHealthCheckAsync(
                container, serviceName, healthConfig, cancellationToken);
            healthStatus = httpResult.Status;
            reason = httpResult.Reason;
        }
        // Fall back to Docker status
        else
        {
            healthStatus = DetermineHealthStatusFromDocker(container);
            reason = DetermineHealthReason(container, healthStatus);
        }

        // Only fetch RestartCount for unhealthy containers
        if (healthStatus != HealthStatus.Healthy && !string.IsNullOrEmpty(container.Id))
        {
            restartCount = await _dockerService.GetContainerRestartCountAsync(
                environmentId, container.Id, cancellationToken);
        }

        return ServiceHealth.Create(
            serviceName,
            healthStatus,
            container.Id,
            container.Name.TrimStart('/'),
            reason,
            restartCount);
    }

    /// <summary>
    /// Performs HTTP health check for a container.
    /// </summary>
    private async Task<(HealthStatus Status, string? Reason)> PerformHttpHealthCheckAsync(
        ContainerDto container,
        string serviceName,
        ServiceHealthCheckConfig config,
        CancellationToken cancellationToken)
    {
        // Determine container address (use container name as Docker DNS resolves it)
        var containerAddress = container.Name.TrimStart('/');

        // Determine port (use config or first exposed port)
        var port = config.Port ?? GetFirstExposedPort(container);
        if (port == null)
        {
            _logger.LogWarning(
                "No port configured for HTTP health check on service {ServiceName}, falling back to Docker status",
                serviceName);
            return (DetermineHealthStatusFromDocker(container), "No port for HTTP health check");
        }

        var httpConfig = new HttpHealthCheckConfig
        {
            Path = config.Path,
            Port = port.Value,
            Timeout = TimeSpan.FromSeconds(config.TimeoutSeconds),
            HealthyStatusCodes = config.ExpectedStatusCodes,
            UseHttps = config.UseHttps
        };

        try
        {
            var result = await _httpHealthChecker!.CheckHealthAsync(
                containerAddress, httpConfig, cancellationToken);

            if (result.IsHealthy)
            {
                return (HealthStatus.Healthy, null);
            }
            else
            {
                // Map reported status to HealthStatus
                var status = result.ReportedStatus?.ToLowerInvariant() switch
                {
                    "healthy" => HealthStatus.Healthy,
                    "degraded" => HealthStatus.Degraded,
                    "unhealthy" => HealthStatus.Unhealthy,
                    _ => HealthStatus.Unhealthy
                };

                var reason = result.Error ?? $"HTTP health check: {result.ReportedStatus}";
                if (result.ResponseTimeMs.HasValue)
                {
                    reason += $" ({result.ResponseTimeMs}ms)";
                }

                return (status, reason);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "HTTP health check failed for {ServiceName}", serviceName);
            return (HealthStatus.Unhealthy, $"HTTP health check error: {ex.Message}");
        }
    }

    /// <summary>
    /// Gets the first exposed port from a container.
    /// </summary>
    private static int? GetFirstExposedPort(ContainerDto container)
    {
        var firstPort = container.Ports?.FirstOrDefault();
        if (firstPort != null && firstPort.PrivatePort > 0)
        {
            return firstPort.PrivatePort;
        }
        return null;
    }

    /// <summary>
    /// Determines if a container belongs to the specified stack.
    /// </summary>
    private static bool BelongsToStack(ContainerDto container, string stackName)
    {
        // Check by ReadyStackGo stack label (primary)
        if (container.Labels.TryGetValue("rsgo.stack", out var rsgoStack))
        {
            return string.Equals(rsgoStack, stackName, StringComparison.OrdinalIgnoreCase);
        }

        // Check by Docker Compose project label (for compose-deployed stacks)
        if (container.Labels.TryGetValue("com.docker.compose.project", out var project))
        {
            return string.Equals(project, stackName, StringComparison.OrdinalIgnoreCase);
        }

        // No label-based matching available - do NOT fall back to name prefix matching
        // as this causes false positives with similarly named stacks
        return false;
    }

    /// <summary>
    /// Extracts the service name from a container.
    /// </summary>
    private static string ExtractServiceName(ContainerDto container)
    {
        // Try to get service name from Docker Compose label
        if (container.Labels.TryGetValue("com.docker.compose.service", out var service))
        {
            return service;
        }

        // Fall back to container name
        return container.Name.TrimStart('/');
    }

    /// <summary>
    /// Determines the health status based on Docker container state and health check.
    /// Init containers (lifecycle=init) in "exited" state are considered healthy if they exited successfully (exit code 0).
    /// </summary>
    private static HealthStatus DetermineHealthStatusFromDocker(ContainerDto container)
    {
        // First check Docker health status if available
        if (!string.IsNullOrEmpty(container.HealthStatus) && container.HealthStatus != "none")
        {
            return container.HealthStatus.ToLowerInvariant() switch
            {
                "healthy" => HealthStatus.Healthy,
                "unhealthy" => HealthStatus.Unhealthy,
                "starting" => HealthStatus.Degraded,
                _ => HealthStatus.Unknown
            };
        }

        // Special handling for init containers (run-once containers like database migrators)
        var isInitContainer = container.Labels.TryGetValue("rsgo.lifecycle", out var lifecycle) &&
                              lifecycle.Equals("init", StringComparison.OrdinalIgnoreCase);

        if (isInitContainer && container.State.Equals("exited", StringComparison.OrdinalIgnoreCase))
        {
            // Init containers are expected to exit after completion
            // Check the exit code from the Status field (e.g., "Exited (0) 5 minutes ago")
            if (container.Status.Contains("Exited (0)", StringComparison.OrdinalIgnoreCase))
            {
                return HealthStatus.Healthy; // Successful completion
            }
            else
            {
                return HealthStatus.Unhealthy; // Failed with non-zero exit code
            }
        }

        // Fall back to container state for regular services
        return container.State.ToLowerInvariant() switch
        {
            "running" => HealthStatus.Healthy,
            "restarting" => HealthStatus.Degraded,
            "paused" => HealthStatus.Degraded,
            "exited" => HealthStatus.Unhealthy,
            "dead" => HealthStatus.Unhealthy,
            "created" => HealthStatus.Unknown,
            _ => HealthStatus.Unknown
        };
    }

    /// <summary>
    /// Determines the reason for the current health status.
    /// </summary>
    private static string? DetermineHealthReason(ContainerDto container, HealthStatus status)
    {
        if (status == HealthStatus.Healthy)
            return null;

        if (!string.IsNullOrEmpty(container.HealthStatus) && container.HealthStatus != "none")
        {
            if (container.HealthStatus.Equals("unhealthy", StringComparison.OrdinalIgnoreCase))
                return $"Health check failing (streak: {container.FailingStreak})";

            if (container.HealthStatus.Equals("starting", StringComparison.OrdinalIgnoreCase))
                return "Container starting, health check pending";
        }

        return container.State.ToLowerInvariant() switch
        {
            "restarting" => "Container is restarting",
            "paused" => "Container is paused",
            "exited" => $"Container exited (status: {container.Status})",
            "dead" => "Container is dead",
            "created" => "Container created but not started",
            _ => $"Unknown state: {container.State}"
        };
    }

    /// <summary>
    /// Gets the operation mode from the deployment.
    /// The OperationMode is now stored directly on the Deployment aggregate.
    /// </summary>
    private static OperationMode DetermineOperationMode(Deployment? deployment)
    {
        if (deployment == null)
            return OperationMode.Normal;

        // Use the stored OperationMode from the deployment
        return deployment.OperationMode;
    }
}
