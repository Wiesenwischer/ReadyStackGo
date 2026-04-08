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
    private readonly IHealthCheckStrategyFactory _strategyFactory;
    private readonly ILogger<HealthMonitoringService> _logger;

    public HealthMonitoringService(
        IDockerService dockerService,
        IDeploymentRepository deploymentRepository,
        IHealthSnapshotRepository healthSnapshotRepository,
        IHealthCheckStrategyFactory strategyFactory,
        ILogger<HealthMonitoringService> logger)
    {
        _dockerService = dockerService;
        _deploymentRepository = deploymentRepository;
        _healthSnapshotRepository = healthSnapshotRepository;
        _strategyFactory = strategyFactory;
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

        // Detect missing services: compare expected (deployment.Services) vs found containers
        if (deployment?.Services.Count > 0)
        {
            var foundServiceNames = selfHealth.Services
                .Select(s => s.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var missingServices = deployment.Services
                .Where(s => !foundServiceNames.Contains(s.ServiceName))
                .Select(s => ServiceHealth.NotFound(s.ServiceName))
                .ToList();

            if (missingServices.Count > 0)
            {
                _logger.LogWarning(
                    "Missing containers for stack {StackName}: {MissingServices}",
                    stackName, string.Join(", ", missingServices.Select(s => s.Name)));

                selfHealth = SelfHealth.Create(selfHealth.Services.Concat(missingServices));
            }
        }

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

    public Task<IEnumerable<HealthSnapshot>> GetHealthTransitionsAsync(
        DeploymentId deploymentId,
        CancellationToken cancellationToken = default)
    {
        var transitions = _healthSnapshotRepository.GetTransitions(deploymentId);
        return Task.FromResult(transitions);
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

            // Filter containers belonging to this stack, excluding init containers
            // Init containers (lifecycle=init) are run-once containers (e.g., database migrators)
            // that exit after completion and should not appear in health monitoring
            var stackContainers = containers
                .Where(c => BelongsToStack(c, stackName))
                .Where(c => !IsInitContainer(c))
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
    /// Delegates to the appropriate health check strategy based on config type.
    /// </summary>
    private async Task<ServiceHealth> CollectServiceHealthAsync(
        ContainerDto container,
        string serviceName,
        ServiceHealthCheckConfig? healthConfig,
        string environmentId,
        CancellationToken cancellationToken)
    {
        HealthCheckStrategyResult result;

        // Non-running containers always use Docker status — active probing doesn't make sense
        if (container.State.ToLowerInvariant() != "running")
        {
            result = DockerHealthCheckStrategy.FromDocker(container);
        }
        else
        {
            var strategy = _strategyFactory.GetStrategy(healthConfig?.Type ?? "docker");
            result = await strategy.CheckHealthAsync(container, serviceName,
                healthConfig ?? ServiceHealthCheckConfig.Docker(), cancellationToken);
        }

        // Only fetch RestartCount for non-healthy containers (not for Healthy or Running)
        int? restartCount = null;
        if (result.Status != HealthStatus.Healthy && result.Status != HealthStatus.Running
            && !string.IsNullOrEmpty(container.Id))
        {
            restartCount = await _dockerService.GetContainerRestartCountAsync(
                environmentId, container.Id, cancellationToken);
        }

        return ServiceHealth.Create(
            serviceName,
            result.Status,
            container.Id,
            container.Name.TrimStart('/'),
            result.Reason,
            restartCount,
            result.Entries,
            result.ResponseTimeMs);
    }

    /// <summary>
    /// Determines if a container is an init container (lifecycle=init).
    /// Init containers are excluded from health monitoring.
    /// </summary>
    private static bool IsInitContainer(ContainerDto container) =>
        container.Labels.TryGetValue("rsgo.lifecycle", out var lifecycle) &&
        lifecycle.Equals("init", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines if a container belongs to the specified stack.
    /// </summary>
    private static bool BelongsToStack(ContainerDto container, string stackName)
    {
        if (container.Labels.TryGetValue("rsgo.stack", out var rsgoStack))
            return string.Equals(rsgoStack, stackName, StringComparison.OrdinalIgnoreCase);

        if (container.Labels.TryGetValue("com.docker.compose.project", out var project))
            return string.Equals(project, stackName, StringComparison.OrdinalIgnoreCase);

        return false;
    }

    /// <summary>
    /// Extracts the service name from a container.
    /// </summary>
    private static string ExtractServiceName(ContainerDto container)
    {
        if (container.Labels.TryGetValue("com.docker.compose.service", out var service))
            return service;

        return container.Name.TrimStart('/');
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
