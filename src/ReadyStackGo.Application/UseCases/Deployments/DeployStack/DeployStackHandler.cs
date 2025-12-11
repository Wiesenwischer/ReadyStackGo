using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

public class DeployStackHandler : IRequestHandler<DeployStackCommand, DeployStackResponse>
{
    private readonly IStackSourceService _stackSourceService;
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly TimeProvider _timeProvider;

    public DeployStackHandler(
        IStackSourceService stackSourceService,
        IDeploymentService deploymentService,
        IDeploymentNotificationService? notificationService = null,
        TimeProvider? timeProvider = null)
    {
        _stackSourceService = stackSourceService;
        _deploymentService = deploymentService;
        _notificationService = notificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DeployStackResponse> Handle(DeployStackCommand request, CancellationToken cancellationToken)
    {
        // Load stack definition from catalog via application service
        var stackDefinition = await _stackSourceService.GetStackAsync(request.StackId, cancellationToken);
        if (stackDefinition == null)
        {
            return DeployStackResponse.Failed(
                $"Stack '{request.StackId}' not found in catalog",
                $"Stack '{request.StackId}' not found");
        }

        // Create request DTO from domain object (mapping at application layer boundary)
        var deployRequest = new DeployStackRequest
        {
            StackName = request.StackName,
            Services = stackDefinition.Services,
            Volumes = stackDefinition.Volumes,
            Networks = stackDefinition.Networks,
            StackVersion = stackDefinition.ProductVersion,
            Variables = request.Variables,
            EnvironmentId = request.EnvironmentId,
            CatalogStackId = request.StackId
        };

        // Use client-provided session ID if available, otherwise generate one
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"{request.StackName}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        // Create progress callback that sends notifications via SignalR
        DeploymentServiceProgressCallback? progressCallback = null;
        if (_notificationService != null)
        {
            progressCallback = async (phase, message, percent, currentService, totalServices, completedServices) =>
            {
                await _notificationService.NotifyProgressAsync(
                    sessionId,
                    phase,
                    message,
                    percent,
                    currentService,
                    totalServices,
                    completedServices,
                    cancellationToken);
            };
        }

        var result = await _deploymentService.DeployStackAsync(
            request.EnvironmentId,
            deployRequest,
            progressCallback,
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
