namespace ReadyStackGo.Infrastructure.Services;

using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.IdentityAccess.Organizations;

/// <summary>
/// Service for monitoring health of deployed stacks.
/// Collects container status from Docker and aggregates into health snapshots.
/// </summary>
public class HealthMonitoringService : IHealthMonitoringService
{
    private readonly IDockerService _dockerService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly ILogger<HealthMonitoringService> _logger;

    public HealthMonitoringService(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IHealthSnapshotRepository healthSnapshotRepository,
        ILogger<HealthMonitoringService> logger)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _healthSnapshotRepository = healthSnapshotRepository;
        _logger = logger;
    }

    public async Task<HealthSnapshot> CaptureHealthSnapshotAsync(
        OrganizationId organizationId,
        EnvironmentId environmentId,
        DeploymentId deploymentId,
        string stackName,
        string? currentVersion = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Capturing health snapshot for deployment {DeploymentId}", deploymentId);

        // Get current deployment to determine operation mode
        var deployment = _deploymentRepository.Get(deploymentId);
        var operationMode = DetermineOperationMode(deployment);

        // Collect container health from Docker
        var selfHealth = await CollectSelfHealthAsync(
            environmentId.Value.ToString(),
            stackName,
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
    /// </summary>
    private async Task<SelfHealth> CollectSelfHealthAsync(
        string environmentId,
        string stackName,
        CancellationToken cancellationToken)
    {
        try
        {
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

            var serviceHealthList = stackContainers
                .Select(c => MapContainerToServiceHealth(c))
                .ToList();

            return SelfHealth.Create(serviceHealthList);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect container health for stack {StackName}", stackName);
            return SelfHealth.Empty();
        }
    }

    /// <summary>
    /// Determines if a container belongs to the specified stack.
    /// </summary>
    private static bool BelongsToStack(ContainerDto container, string stackName)
    {
        // Check by Docker Compose project label
        if (container.Labels.TryGetValue("com.docker.compose.project", out var project))
        {
            return string.Equals(project, stackName, StringComparison.OrdinalIgnoreCase);
        }

        // Fallback: check by container name prefix
        var normalizedStackName = stackName.ToLowerInvariant().Replace(".", "-").Replace("_", "-");
        var normalizedContainerName = container.Name.TrimStart('/').ToLowerInvariant();

        return normalizedContainerName.StartsWith(normalizedStackName);
    }

    /// <summary>
    /// Maps a Docker container DTO to a ServiceHealth value object.
    /// </summary>
    private static ServiceHealth MapContainerToServiceHealth(ContainerDto container)
    {
        var healthStatus = DetermineHealthStatus(container);
        var reason = DetermineHealthReason(container, healthStatus);

        // Extract service name from container (remove stack prefix if present)
        var serviceName = ExtractServiceName(container);

        return ServiceHealth.Create(
            serviceName,
            healthStatus,
            container.Id,
            container.Name.TrimStart('/'),
            reason,
            restartCount: 0); // TODO: Get restart count from Docker inspect
    }

    /// <summary>
    /// Determines the health status based on container state and health check.
    /// </summary>
    private static HealthStatus DetermineHealthStatus(ContainerDto container)
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

        // Fall back to container state
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
    /// Determines the operation mode based on deployment status.
    /// </summary>
    private static OperationMode DetermineOperationMode(Deployment? deployment)
    {
        if (deployment == null)
            return OperationMode.Normal;

        return deployment.Status switch
        {
            DeploymentStatus.Running => OperationMode.Normal,
            DeploymentStatus.Stopped => OperationMode.Stopped,
            DeploymentStatus.Failed => OperationMode.Failed,
            DeploymentStatus.Pending => OperationMode.Migrating,
            _ => OperationMode.Normal
        };
    }
}
