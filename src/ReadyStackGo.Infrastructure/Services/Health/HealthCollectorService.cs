using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.ProductDeployments;
using DomainDeployment = ReadyStackGo.Domain.Deployment.Deployments.Deployment;
using DomainHealthCheckConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig.ServiceHealthCheckConfig;
using AppHealthCheckConfig = ReadyStackGo.Application.Services.ServiceHealthCheckConfig;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Service that coordinates health data collection across all deployments.
/// Used by the background service for periodic health checks.
/// </summary>
public class HealthCollectorService : IHealthCollectorService
{
    private readonly IHealthMonitoringService _healthMonitoringService;
    private readonly IHealthNotificationService _healthNotificationService;
    private readonly IHealthChangeTracker _healthChangeTracker;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly ILogger<HealthCollectorService> _logger;

    public HealthCollectorService(
        IHealthMonitoringService healthMonitoringService,
        IHealthNotificationService healthNotificationService,
        IHealthChangeTracker healthChangeTracker,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        IProductDeploymentRepository productDeploymentRepository,
        ILogger<HealthCollectorService> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _healthNotificationService = healthNotificationService;
        _healthChangeTracker = healthChangeTracker;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
        _productDeploymentRepository = productDeploymentRepository;
        _logger = logger;
    }

    public async Task CollectEnvironmentHealthAsync(
        EnvironmentId environmentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collecting health for environment {EnvironmentId}", environmentId);

        var environment = _environmentRepository.Get(environmentId);
        if (environment == null)
        {
            _logger.LogWarning("Environment {EnvironmentId} not found", environmentId);
            return;
        }

        var allDeployments = _deploymentRepository.GetByEnvironment(environmentId).ToList();

        // Non-Running deployments (Installing/Upgrading/Failed/Removed) skip health collection,
        // but we reset the tracker baseline so the first cycle after they return to Running
        // doesn't compare current status against a stale pre-upgrade baseline and fire
        // spurious HealthChange notifications.
        foreach (var nonRunning in allDeployments.Where(d => d.Status != DeploymentStatus.Running))
        {
            await _healthChangeTracker.ResetBaselineAsync(
                nonRunning.Id.Value.ToString(),
                cancellationToken);
        }

        var deployments = allDeployments.Where(d => d.Status == DeploymentStatus.Running).ToList();

        if (!deployments.Any())
        {
            _logger.LogDebug("No active deployments in environment {EnvironmentName}", environment.Name);
            return;
        }

        var stackHealthDtos = new List<StackHealthDto>();

        foreach (var deployment in deployments)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                // Map health check configs from deployment to application layer format.
                // For SSH tunnel environments, skip HTTP health checks — RSGO cannot resolve
                // container hostnames on the remote Docker network. Use Docker-native status only.
                var serviceHealthConfigs = environment.Type == EnvironmentType.SshTunnel
                    ? null
                    : MapHealthCheckConfigs(deployment.HealthCheckConfigs);

                var snapshot = await _healthMonitoringService.CaptureHealthSnapshotAsync(
                    environment.OrganizationId,
                    environmentId,
                    deployment.Id,
                    deployment.StackName,
                    deployment.StackVersion,
                    serviceHealthConfigs,
                    cancellationToken);

                var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, environmentId);
                EnrichWithProductInfo(dto, deployment.Id);
                stackHealthDtos.Add(dto);

                // Track health changes and create in-app notifications.
                // Suppress during install/upgrade so the per-product result is the
                // sole signal the user sees for the deploy.
                var serviceStatuses = dto.Self.Services
                    .Select(s => new ServiceHealthUpdate(s.Name, s.Status))
                    .ToList();
                await _healthChangeTracker.ProcessHealthUpdateAsync(
                    deployment.Id.Value.ToString(),
                    deployment.StackName,
                    serviceStatuses,
                    deployment.IsInProgress,
                    cancellationToken);

                // Notify about individual deployment health change (SignalR real-time)
                await _healthNotificationService.NotifyDeploymentHealthChangedAsync(
                    deployment.Id,
                    dto,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect health for deployment {DeploymentId}", deployment.Id);
            }
        }

        // Notify about environment-wide health update
        if (stackHealthDtos.Count > 0)
        {
            var environmentSummary = new EnvironmentHealthSummaryDto
            {
                EnvironmentId = environmentId.Value.ToString(),
                EnvironmentName = environment.Name,
                TotalStacks = stackHealthDtos.Count,
                HealthyCount = stackHealthDtos.Count(s => s.OverallStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase)),
                DegradedCount = stackHealthDtos.Count(s => s.OverallStatus.Equals("Degraded", StringComparison.OrdinalIgnoreCase)),
                UnhealthyCount = stackHealthDtos.Count(s => s.OverallStatus.Equals("Unhealthy", StringComparison.OrdinalIgnoreCase)),
                Stacks = stackHealthDtos
            };

            await _healthNotificationService.NotifyEnvironmentHealthChangedAsync(
                environmentId,
                environmentSummary,
                cancellationToken);
        }

        _logger.LogInformation(
            "Collected health for {Count} deployments in environment {EnvironmentName}",
            stackHealthDtos.Count, environment.Name);
    }

    public async Task CollectDeploymentHealthAsync(
        DeploymentId deploymentId,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Collecting health for deployment {DeploymentId}", deploymentId);

        var deployment = _deploymentRepository.Get(deploymentId);
        if (deployment == null)
        {
            _logger.LogWarning("Deployment {DeploymentId} not found", deploymentId);
            return;
        }

        // Only collect health for running deployments. Reset the tracker baseline
        // so the first post-recovery cycle doesn't fire stale transition notifications.
        if (deployment.Status != DeploymentStatus.Running)
        {
            await _healthChangeTracker.ResetBaselineAsync(deploymentId.Value.ToString(), cancellationToken);
            _logger.LogDebug("Skipping health collection for deployment {DeploymentId} with status {Status}",
                deploymentId, deployment.Status);
            return;
        }

        var environment = _environmentRepository.Get(deployment.EnvironmentId);
        if (environment == null)
        {
            _logger.LogWarning("Environment {EnvironmentId} not found for deployment {DeploymentId}",
                deployment.EnvironmentId, deploymentId);
            return;
        }

        try
        {
            // Map health check configs from deployment to application layer format
            var serviceHealthConfigs = MapHealthCheckConfigs(deployment.HealthCheckConfigs);

            var snapshot = await _healthMonitoringService.CaptureHealthSnapshotAsync(
                environment.OrganizationId,
                deployment.EnvironmentId,
                deploymentId,
                deployment.StackName,
                deployment.StackVersion,
                serviceHealthConfigs,
                cancellationToken);

            var dto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, deployment.EnvironmentId);
            EnrichWithProductInfo(dto, deploymentId);

            // Track health changes and create in-app notifications.
            // Suppress during install/upgrade (defense in depth — the Running-only
            // filter above already blocks this path, but keep the signal explicit).
            var serviceStatuses = dto.Self.Services
                .Select(s => new ServiceHealthUpdate(s.Name, s.Status))
                .ToList();
            await _healthChangeTracker.ProcessHealthUpdateAsync(
                deploymentId.Value.ToString(),
                deployment.StackName,
                serviceStatuses,
                deployment.IsInProgress,
                cancellationToken);

            // Notify about deployment health change (SignalR real-time)
            await _healthNotificationService.NotifyDeploymentHealthChangedAsync(
                deploymentId,
                dto,
                cancellationToken);

            _logger.LogInformation(
                "Collected health for deployment {StackName}: {OverallStatus}",
                deployment.StackName, snapshot.Overall.Name);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to collect health for deployment {DeploymentId}", deploymentId);
        }
    }

    public async Task CollectAllHealthAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting health collection for all environments");

        // Get all active deployments grouped by environment
        var allDeployments = GetAllActiveDeployments();
        var environmentIds = allDeployments
            .Select(d => d.EnvironmentId)
            .Distinct()
            .ToList();

        _logger.LogInformation("Collecting health for {Count} environments with active deployments", environmentIds.Count);

        foreach (var environmentId in environmentIds)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await CollectEnvironmentHealthAsync(environmentId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect health for environment {EnvironmentId}", environmentId);
            }
        }

        _logger.LogDebug("Completed health collection cycle");
    }

    private IEnumerable<DomainDeployment> GetAllActiveDeployments()
    {
        return _deploymentRepository.GetAllActive();
    }

    /// <summary>
    /// Maps domain health check configs to application layer format.
    /// </summary>
    private static IReadOnlyDictionary<string, AppHealthCheckConfig>? MapHealthCheckConfigs(
        IReadOnlyCollection<DomainHealthCheckConfig>? domainConfigs)
    {
        if (domainConfigs == null || domainConfigs.Count == 0)
            return null;

        var result = new Dictionary<string, AppHealthCheckConfig>();

        foreach (var config in domainConfigs)
        {
            // Parse timeout from string (e.g., "00:00:10") to seconds
            var timeoutSeconds = 5;
            if (!string.IsNullOrEmpty(config.Timeout) && TimeSpan.TryParse(config.Timeout, out var timeout))
            {
                timeoutSeconds = (int)timeout.TotalSeconds;
            }

            var appConfig = new AppHealthCheckConfig
            {
                Type = config.Type,
                Path = config.Path ?? "/hc",
                Port = config.Port,
                TimeoutSeconds = timeoutSeconds,
                UseHttps = config.Https,
                ExpectedStatusCodes = config.ExpectedStatusCodes ?? new[] { 200 }
            };

            result[config.ServiceName] = appConfig;
        }

        return result.Count > 0 ? result : null;
    }

    private void EnrichWithProductInfo(StackHealthDto dto, DeploymentId deploymentId)
    {
        var productDeployment = _productDeploymentRepository.GetByStackDeploymentId(deploymentId);
        if (productDeployment != null && !productDeployment.IsTerminal)
        {
            dto.ProductDeploymentId = productDeployment.Id.Value.ToString();
            dto.ProductDisplayName = productDeployment.ProductDisplayName;
        }
    }
}
