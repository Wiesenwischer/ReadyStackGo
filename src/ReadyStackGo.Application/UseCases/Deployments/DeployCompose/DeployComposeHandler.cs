using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployCompose;

public class DeployComposeHandler : IRequestHandler<DeployComposeCommand, DeployComposeResponse>
{
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly TimeProvider _timeProvider;

    public DeployComposeHandler(
        IDeploymentService deploymentService,
        IDeploymentNotificationService? notificationService = null,
        TimeProvider? timeProvider = null)
    {
        _deploymentService = deploymentService;
        _notificationService = notificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DeployComposeResponse> Handle(DeployComposeCommand request, CancellationToken cancellationToken)
    {
        var deployRequest = new DeployComposeRequest
        {
            StackName = request.StackName,
            YamlContent = request.YamlContent,
            StackVersion = request.StackVersion,
            Variables = request.Variables
        };

        // Use client-provided session ID if available, otherwise generate one
        // Client-provided session ID allows subscribing to SignalR BEFORE calling this endpoint
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"{request.StackName}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // Create progress callback that sends notifications via SignalR
        DeploymentServiceProgressCallback? progressCallback = null;
        if (_notificationService != null)
        {
            progressCallback = async (phase, message, percent, currentService, totalServices, completedServices, totalInitContainers, completedInitContainers) =>
            {
                await _notificationService.NotifyProgressAsync(
                    sessionId,
                    phase,
                    message,
                    percent,
                    currentService,
                    totalServices,
                    completedServices,
                    totalInitContainers,
                    completedInitContainers,
                    cancellationToken);
            };
        }

        // Create log callback for init container log streaming
        InitContainerLogCallback? logCallback = null;
        if (_notificationService != null)
        {
            logCallback = async (containerName, logLine) =>
            {
                await _notificationService.NotifyInitContainerLogAsync(
                    sessionId, containerName, logLine, cancellationToken);
            };
        }

        var result = await _deploymentService.DeployComposeAsync(
            request.EnvironmentId,
            deployRequest,
            progressCallback,
            logCallback,
            cancellationToken);

        // Send final notification
        if (_notificationService != null)
        {
            if (result.Success)
            {
                await _notificationService.NotifyCompletedAsync(
                    sessionId,
                    result.Message ?? "Deployment completed",
                    result.Services?.Count ?? 0,
                    cancellationToken);
            }
            else
            {
                await _notificationService.NotifyErrorAsync(
                    sessionId,
                    result.Message ?? "Deployment failed",
                    null,
                    result.Services?.Count ?? 0,
                    0,
                    cancellationToken);
            }
        }

        // Include session ID in response for client reference
        result.DeploymentSessionId = sessionId;
        return result;
    }
}
