using Microsoft.AspNetCore.SignalR;
using ReadyStackGo.Api.Hubs;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.Services;

/// <summary>
/// Implementation of IDeploymentNotificationService using SignalR.
/// Sends real-time deployment progress updates to connected clients.
/// </summary>
public class DeploymentNotificationService : IDeploymentNotificationService
{
    private readonly IHubContext<DeploymentHub> _hubContext;
    private readonly ILogger<DeploymentNotificationService> _logger;

    public DeploymentNotificationService(
        IHubContext<DeploymentHub> hubContext,
        ILogger<DeploymentNotificationService> logger)
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    public async Task NotifyProgressAsync(
        string deploymentId,
        string phase,
        string message,
        int progressPercent,
        string? currentService = null,
        int totalServices = 0,
        int completedServices = 0,
        CancellationToken cancellationToken = default)
    {
        var update = new DeploymentProgressUpdate
        {
            DeploymentId = deploymentId,
            Phase = phase,
            Message = message,
            ProgressPercent = progressPercent,
            CurrentService = currentService,
            TotalServices = totalServices,
            CompletedServices = completedServices,
            IsComplete = false,
            IsError = false
        };

        await SendUpdateAsync(deploymentId, update, cancellationToken);
    }

    public async Task NotifyCompletedAsync(
        string deploymentId,
        string message,
        int totalServices,
        CancellationToken cancellationToken = default)
    {
        var update = new DeploymentProgressUpdate
        {
            DeploymentId = deploymentId,
            Phase = "Complete",
            Message = message,
            ProgressPercent = 100,
            TotalServices = totalServices,
            CompletedServices = totalServices,
            IsComplete = true,
            IsError = false
        };

        await SendUpdateAsync(deploymentId, update, cancellationToken);
    }

    public async Task NotifyErrorAsync(
        string deploymentId,
        string errorMessage,
        string? currentService = null,
        int totalServices = 0,
        int completedServices = 0,
        CancellationToken cancellationToken = default)
    {
        var update = new DeploymentProgressUpdate
        {
            DeploymentId = deploymentId,
            Phase = "Error",
            Message = "Deployment failed",
            ProgressPercent = (totalServices > 0) ? (completedServices * 100 / totalServices) : 0,
            CurrentService = currentService,
            TotalServices = totalServices,
            CompletedServices = completedServices,
            IsComplete = true,
            IsError = true,
            ErrorMessage = errorMessage
        };

        await SendUpdateAsync(deploymentId, update, cancellationToken);
    }

    private async Task SendUpdateAsync(
        string deploymentId,
        DeploymentProgressUpdate update,
        CancellationToken cancellationToken)
    {
        var groupName = $"deployment:{deploymentId}";

        _logger.LogDebug(
            "Sending deployment progress for {DeploymentId}: {Phase} - {Message} ({Percent}%)",
            deploymentId, update.Phase, update.Message, update.ProgressPercent);

        // Send to specific deployment subscribers
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentProgress", update, cancellationToken);

        // Also send to global deployment subscribers
        await _hubContext.Clients
            .Group("deployments:all")
            .SendAsync("DeploymentProgress", update, cancellationToken);
    }
}
