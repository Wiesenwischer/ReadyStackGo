using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.StackSources;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

public class DeployStackHandler : IRequestHandler<DeployStackCommand, DeployStackResponse>
{
    private readonly IStackDefinitionRepository _stackRepository;
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly TimeProvider _timeProvider;

    public DeployStackHandler(
        IStackDefinitionRepository stackRepository,
        IDeploymentService deploymentService,
        IDeploymentNotificationService? notificationService = null,
        TimeProvider? timeProvider = null)
    {
        _stackRepository = stackRepository;
        _deploymentService = deploymentService;
        _notificationService = notificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<DeployStackResponse> Handle(DeployStackCommand request, CancellationToken cancellationToken)
    {
        // Load stack definition from catalog by ID
        var stackDefinition = await _stackRepository.GetByIdAsync(request.StackId, cancellationToken);
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
            YamlContent = stackDefinition.YamlContent,
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
