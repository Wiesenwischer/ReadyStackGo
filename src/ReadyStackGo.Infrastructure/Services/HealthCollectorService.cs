using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Service that coordinates health data collection across all deployments.
/// Used by the background service for periodic health checks.
/// </summary>
public class HealthCollectorService : IHealthCollectorService
{
    private readonly IHealthMonitoringService _healthMonitoringService;
    private readonly IHealthNotificationService _healthNotificationService;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IEnvironmentRepository _environmentRepository;
    private readonly ILogger<HealthCollectorService> _logger;

    public HealthCollectorService(
        IHealthMonitoringService healthMonitoringService,
        IHealthNotificationService healthNotificationService,
        IDeploymentRepository deploymentRepository,
        IEnvironmentRepository environmentRepository,
        ILogger<HealthCollectorService> logger)
    {
        _healthMonitoringService = healthMonitoringService;
        _healthNotificationService = healthNotificationService;
        _deploymentRepository = deploymentRepository;
        _environmentRepository = environmentRepository;
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

        var deployments = _deploymentRepository.GetByEnvironment(environmentId)
            .Where(d => d.Status == DeploymentStatus.Running || d.Status == DeploymentStatus.Stopped)
            .ToList();

        if (!deployments.Any())
        {
            _logger.LogDebug("No active deployments in environment {EnvironmentName}", environment.Name);
            return;
        }

        var stackSummaries = new List<StackHealthSummaryDto>();

        foreach (var deployment in deployments)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                var snapshot = await _healthMonitoringService.CaptureHealthSnapshotAsync(
                    environment.OrganizationId,
                    environmentId,
                    deployment.Id,
                    deployment.StackName,
                    deployment.StackVersion,
                    serviceHealthConfigs: null, // TODO: Load from stack definition
                    cancellationToken);

                var summaryDto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);
                stackSummaries.Add(summaryDto);

                // Notify about individual deployment health change (summary for environment list)
                await _healthNotificationService.NotifyDeploymentHealthChangedAsync(
                    deployment.Id,
                    summaryDto,
                    cancellationToken);

                // Notify detailed health for deployment detail view subscribers
                var detailedDto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, environmentId);
                await _healthNotificationService.NotifyDeploymentDetailedHealthChangedAsync(
                    deployment.Id,
                    detailedDto,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to collect health for deployment {DeploymentId}", deployment.Id);
            }
        }

        // Notify about environment-wide health update
        if (stackSummaries.Any())
        {
            var environmentSummary = new EnvironmentHealthSummaryDto
            {
                EnvironmentId = environmentId.Value.ToString(),
                EnvironmentName = environment.Name,
                TotalStacks = stackSummaries.Count,
                HealthyCount = stackSummaries.Count(s => s.OverallStatus.Equals("Healthy", StringComparison.OrdinalIgnoreCase)),
                DegradedCount = stackSummaries.Count(s => s.OverallStatus.Equals("Degraded", StringComparison.OrdinalIgnoreCase)),
                UnhealthyCount = stackSummaries.Count(s => s.OverallStatus.Equals("Unhealthy", StringComparison.OrdinalIgnoreCase)),
                Stacks = stackSummaries
            };

            await _healthNotificationService.NotifyEnvironmentHealthChangedAsync(
                environmentId,
                environmentSummary,
                cancellationToken);
        }

        _logger.LogInformation(
            "Collected health for {Count} deployments in environment {EnvironmentName}",
            stackSummaries.Count, environment.Name);
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

        // Only collect health for active deployments
        if (deployment.Status != DeploymentStatus.Running && deployment.Status != DeploymentStatus.Stopped)
        {
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
            var snapshot = await _healthMonitoringService.CaptureHealthSnapshotAsync(
                environment.OrganizationId,
                deployment.EnvironmentId,
                deploymentId,
                deployment.StackName,
                deployment.StackVersion,
                serviceHealthConfigs: null, // TODO: Load from stack definition
                cancellationToken);

            var summaryDto = HealthSnapshotMapper.MapToStackHealthSummary(snapshot);

            // Notify about deployment health change (summary)
            await _healthNotificationService.NotifyDeploymentHealthChangedAsync(
                deploymentId,
                summaryDto,
                cancellationToken);

            // Notify detailed health for deployment detail view subscribers
            var detailedDto = HealthSnapshotMapper.MapToStackHealthDto(snapshot, deployment.EnvironmentId);
            await _healthNotificationService.NotifyDeploymentDetailedHealthChangedAsync(
                deploymentId,
                detailedDto,
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

    private IEnumerable<Deployment> GetAllActiveDeployments()
    {
        return _deploymentRepository.GetAllActive();
    }
}
