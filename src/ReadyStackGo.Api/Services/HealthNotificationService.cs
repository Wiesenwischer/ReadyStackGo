using Microsoft.AspNetCore.SignalR;
using ReadyStackGo.Api.Hubs;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Api.Services;

/// <summary>
/// Implementation of IHealthNotificationService using SignalR.
/// </summary>
public class HealthNotificationService : IHealthNotificationService
{
    private readonly IHubContext<HealthHub> _hubContext;
    private readonly ILogger<HealthNotificationService> _logger;

    public HealthNotificationService(
        IHubContext<HealthHub> hubContext,
        ILogger<HealthNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyDeploymentHealthChangedAsync(
        DeploymentId deploymentId,
        StackHealthSummaryDto healthSummary,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deploy:{deploymentId.Value}";

        _logger.LogDebug(
            "Sending health summary for deployment {DeploymentId} to group {Group}",
            deploymentId, groupName);

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentHealthChanged", healthSummary, cancellationToken);
    }

    public async Task NotifyDeploymentDetailedHealthChangedAsync(
        DeploymentId deploymentId,
        StackHealthDto detailedHealth,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deploy:{deploymentId.Value}";

        _logger.LogDebug(
            "Sending detailed health for deployment {DeploymentId} to group {Group} with {ServiceCount} services",
            deploymentId, groupName, detailedHealth.Self.Services.Count);

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentDetailedHealthChanged", detailedHealth, cancellationToken);
    }

    public async Task NotifyEnvironmentHealthChangedAsync(
        EnvironmentId environmentId,
        EnvironmentHealthSummaryDto summary,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"env:{environmentId.Value}";

        _logger.LogDebug(
            "Sending health update for environment {EnvironmentId} to group {Group}",
            environmentId, groupName);

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("EnvironmentHealthChanged", summary, cancellationToken);
    }

    public async Task NotifyGlobalHealthChangedAsync(
        StackHealthSummaryDto healthSummary,
        CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "Sending global health update for stack {StackName}",
            healthSummary.StackName);

        await _hubContext.Clients
            .Group("health:all")
            .SendAsync("GlobalHealthChanged", healthSummary, cancellationToken);
    }

    public async Task NotifyObserverResultAsync(
        DeploymentId deploymentId,
        string stackName,
        ObserverResultDto result,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deploy:{deploymentId.Value}";

        _logger.LogDebug(
            "Sending observer result for deployment {DeploymentId} ({StackName}): MaintenanceRequired={MaintenanceRequired}",
            deploymentId, stackName, result.IsMaintenanceRequired);

        var payload = new
        {
            DeploymentId = deploymentId.Value.ToString(),
            StackName = stackName,
            Result = result
        };

        // Send to deployment subscribers
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("ObserverResultChanged", payload, cancellationToken);

        // Also send to global health subscribers
        await _hubContext.Clients
            .Group("health:all")
            .SendAsync("ObserverResultChanged", payload, cancellationToken);
    }
}
