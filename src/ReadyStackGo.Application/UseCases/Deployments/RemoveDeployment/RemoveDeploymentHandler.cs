using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

public class RemoveDeploymentHandler : IRequestHandler<RemoveDeploymentCommand, DeployComposeResponse>
{
    private readonly IDeploymentService _deploymentService;

    public RemoveDeploymentHandler(IDeploymentService deploymentService)
    {
        _deploymentService = deploymentService;
    }

    public async Task<DeployComposeResponse> Handle(RemoveDeploymentCommand request, CancellationToken cancellationToken)
    {
        return await _deploymentService.RemoveDeploymentAsync(request.EnvironmentId, request.StackName);
    }
}

public class RemoveDeploymentByIdHandler : IRequestHandler<RemoveDeploymentByIdCommand, DeployComposeResponse>
{
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<RemoveDeploymentByIdHandler> _logger;

    public RemoveDeploymentByIdHandler(
        IDeploymentService deploymentService,
        IDeploymentNotificationService notificationService,
        ILogger<RemoveDeploymentByIdHandler> logger,
        INotificationService? inAppNotificationService = null)
    {
        _deploymentService = deploymentService;
        _notificationService = notificationService;
        _logger = logger;
        _inAppNotificationService = inAppNotificationService;
    }

    public async Task<DeployComposeResponse> Handle(RemoveDeploymentByIdCommand request, CancellationToken cancellationToken)
    {
        // If no session ID, use the simple method without progress
        if (string.IsNullOrEmpty(request.SessionId))
        {
            var simpleResult = await _deploymentService.RemoveDeploymentByIdAsync(request.EnvironmentId, request.DeploymentId);
            await CreateRemoveNotificationAsync(simpleResult, cancellationToken);
            return simpleResult;
        }

        _logger.LogInformation("Removing deployment {DeploymentId} with session {SessionId}",
            request.DeploymentId, request.SessionId);

        // Create progress callback that sends SignalR notifications
        DeploymentServiceProgressCallback progressCallback = async (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
        {
            if (phase == "Complete")
            {
                await _notificationService.NotifyCompletedAsync(
                    request.SessionId,
                    message,
                    totalServices);
            }
            else if (phase == "Error")
            {
                await _notificationService.NotifyErrorAsync(
                    request.SessionId,
                    message,
                    currentService,
                    totalServices,
                    completedServices);
            }
            else
            {
                await _notificationService.NotifyProgressAsync(
                    request.SessionId,
                    phase,
                    message,
                    percent,
                    currentService,
                    totalServices,
                    completedServices,
                    totalInitContainers,
                    completedInitContainers);
            }
        };

        var result = await _deploymentService.RemoveDeploymentByIdAsync(
            request.EnvironmentId,
            request.DeploymentId,
            progressCallback,
            cancellationToken);

        await CreateRemoveNotificationAsync(result, cancellationToken);
        return result;
    }

    private async Task CreateRemoveNotificationAsync(DeployComposeResponse result, CancellationToken ct)
    {
        if (_inAppNotificationService == null || string.IsNullOrEmpty(result.StackName)) return;
        var stackName = result.StackName;

        try
        {
            var notification = NotificationFactory.CreateDeploymentResult(
                result.Success, "remove", stackName, result.Message);

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create remove notification for {StackName}", stackName);
        }
    }
}
