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
        string sessionId,
        string phase,
        string message,
        int percentComplete,
        string? currentService,
        int totalServices,
        int completedServices,
        int totalInitContainers,
        int completedInitContainers,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deployment:{sessionId}";

        _logger.LogInformation(
            "Sending deployment progress for session {SessionId}: {Phase} - {Percent}% (group: {GroupName})",
            sessionId, phase, percentComplete, groupName);

        var payload = new
        {
            SessionId = sessionId,
            Phase = phase,
            Message = message,
            PercentComplete = percentComplete,
            CurrentService = currentService,
            TotalServices = totalServices,
            CompletedServices = completedServices,
            TotalInitContainers = totalInitContainers,
            CompletedInitContainers = completedInitContainers,
            Status = "InProgress"
        };

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentProgress", payload, cancellationToken);
    }

    public async Task NotifyCompletedAsync(
        string sessionId,
        string message,
        int serviceCount,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deployment:{sessionId}";

        _logger.LogInformation(
            "Deployment completed for session {SessionId}: {ServiceCount} services deployed",
            sessionId, serviceCount);

        var payload = new
        {
            SessionId = sessionId,
            Phase = "Complete",
            Message = message,
            PercentComplete = 100,
            TotalServices = serviceCount,
            CompletedServices = serviceCount,
            Status = "Completed"
        };

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentProgress", payload, cancellationToken);

        // Also send final completion event
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentCompleted", payload, cancellationToken);
    }

    public async Task NotifyErrorAsync(
        string sessionId,
        string errorMessage,
        string? failedService,
        int totalServices,
        int completedServices,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deployment:{sessionId}";

        _logger.LogWarning(
            "Deployment failed for session {SessionId}: {ErrorMessage} (failed on: {FailedService})",
            sessionId, errorMessage, failedService ?? "unknown");

        var payload = new
        {
            SessionId = sessionId,
            Phase = "Error",
            Message = errorMessage,
            FailedService = failedService,
            TotalServices = totalServices,
            CompletedServices = completedServices,
            Status = "Failed"
        };

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentProgress", payload, cancellationToken);

        // Also send final error event
        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("DeploymentFailed", payload, cancellationToken);
    }

    public async Task NotifyInitContainerLogAsync(
        string sessionId,
        string containerName,
        string logLine,
        CancellationToken cancellationToken = default)
    {
        var groupName = $"deployment:{sessionId}";

        var payload = new
        {
            SessionId = sessionId,
            ContainerName = containerName,
            LogLine = logLine
        };

        await _hubContext.Clients
            .Group(groupName)
            .SendAsync("InitContainerLog", payload, cancellationToken);
    }
}
