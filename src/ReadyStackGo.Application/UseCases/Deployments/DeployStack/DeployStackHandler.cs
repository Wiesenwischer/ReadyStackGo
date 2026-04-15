using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Stacks;
using RuntimeConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

public class DeployStackHandler : IRequestHandler<DeployStackCommand, DeployStackResponse>
{
    private readonly IProductSourceService _productSourceService;
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly INotificationService? _inAppNotificationService;
    private readonly ILogger<DeployStackHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public DeployStackHandler(
        IProductSourceService productSourceService,
        IDeploymentService deploymentService,
        ILogger<DeployStackHandler> logger,
        IDeploymentNotificationService? notificationService = null,
        TimeProvider? timeProvider = null,
        INotificationService? inAppNotificationService = null)
    {
        _productSourceService = productSourceService;
        _deploymentService = deploymentService;
        _logger = logger;
        _notificationService = notificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _inAppNotificationService = inAppNotificationService;
    }

    public async Task<DeployStackResponse> Handle(DeployStackCommand request, CancellationToken cancellationToken)
    {
        // Load stack definition from catalog via application service
        var stackDefinition = await _productSourceService.GetStackAsync(request.StackId, cancellationToken);
        if (stackDefinition == null)
        {
            return DeployStackResponse.Failed(
                $"Stack '{request.StackId}' not found in catalog",
                $"Stack '{request.StackId}' not found");
        }

        // Get the product to access MaintenanceObserver (stack ID format: sourceId:productName:stackName)
        var productId = GetProductIdFromStackId(request.StackId);
        var product = productId != null
            ? await _productSourceService.GetProductAsync(productId, cancellationToken)
            : null;

        // Map MaintenanceObserver from StackManagement to Deployment domain model
        var observerConfig = MaintenanceObserverConfigMapper.Map(
            product?.MaintenanceObserver, request.Variables);

        // Extract health check configurations from services
        var healthCheckConfigs = ExtractHealthCheckConfigs(stackDefinition.Services);

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
            CatalogStackId = request.StackId,
            MaintenanceObserver = observerConfig,
            HealthCheckConfigs = healthCheckConfigs,
            ProductGroupId = product?.GroupId,
            StackDefinitionName = stackDefinition.Name,
            ExcludeFromStorage = request.ExcludeFromStorage
        };

        // Use client-provided session ID if available, otherwise generate one
        var sessionId = !string.IsNullOrEmpty(request.SessionId)
            ? request.SessionId
            : $"{request.StackName}-{_timeProvider.GetUtcNow():yyyyMMddHHmmssfff}";

        _logger.LogInformation(
            "DeployStackHandler: Deploying {StackName} with sessionId {SessionId}, notificationService available: {HasNotificationService}",
            request.StackName, sessionId, _notificationService != null);

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

        var result = await _deploymentService.DeployStackAsync(
            request.EnvironmentId,
            deployRequest,
            progressCallback,
            logCallback,
            cancellationToken);

        // Send final SignalR + in-app notifications (unless suppressed by parent handler,
        // e.g. during product deployment where the product handler sends its own final event)
        if (!request.SuppressNotification)
        {
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

            await CreateDeploymentNotificationAsync(result, request.StackName, cancellationToken);
        }

        // Include session ID in response for client reference
        result.DeploymentSessionId = sessionId;
        return result;
    }

    private async Task CreateDeploymentNotificationAsync(
        DeployStackResponse result, string stackName, CancellationToken ct)
    {
        if (_inAppNotificationService == null) return;

        try
        {
            var notification = NotificationFactory.CreateDeploymentResult(
                result.Success, "deploy", stackName,
                result.Message, result.DeploymentId);

            await _inAppNotificationService.AddAsync(notification, ct);
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Failed to create deployment notification for {StackName}", stackName);
        }
    }

    /// <summary>
    /// Extracts product ID from stack ID.
    /// Stack ID format: sourceId:productName:stackName (3 parts) or sourceId:stackName (2 parts for legacy)
    /// Product ID format: sourceId:productName
    /// </summary>
    private static string? GetProductIdFromStackId(string stackId)
    {
        var parts = stackId.Split(':');
        if (parts.Length >= 2)
        {
            // For 3-part format: sourceId:productName:stackName -> sourceId:productName
            // For 2-part format: sourceId:stackName -> sourceId:stackName (product = stack name)
            return parts.Length >= 3
                ? $"{parts[0]}:{parts[1]}"
                : stackId;
        }
        return null;
    }

    /// <summary>
    /// Extracts health check configurations from service templates.
    /// Maps ServiceHealthCheck (StackManagement) to ServiceHealthCheckConfig (Deployment).
    /// </summary>
    private static IReadOnlyList<RuntimeConfig.ServiceHealthCheckConfig>? ExtractHealthCheckConfigs(
        IReadOnlyList<ServiceTemplate> services)
    {
        var configs = new List<RuntimeConfig.ServiceHealthCheckConfig>();

        foreach (var service in services)
        {
            if (service.HealthCheck == null)
                continue;

            // Map ServiceHealthCheck from StackManagement to ServiceHealthCheckConfig in Deployment domain
            var config = new RuntimeConfig.ServiceHealthCheckConfig
            {
                ServiceName = service.Name,
                Type = service.HealthCheck.Type,
                Path = service.HealthCheck.Path,
                Port = service.HealthCheck.Port,
                ExpectedStatusCodes = service.HealthCheck.ExpectedStatusCodes,
                Https = service.HealthCheck.Https,
                Interval = service.HealthCheck.Interval?.ToString(),
                Timeout = service.HealthCheck.Timeout?.ToString(),
                Retries = service.HealthCheck.Retries
            };

            configs.Add(config);
        }

        return configs.Count > 0 ? configs.AsReadOnly() : null;
    }
}
