using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.StackManagement.Manifests;
using ReadyStackGo.Domain.StackManagement.Stacks;
using RuntimeConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig;

namespace ReadyStackGo.Application.UseCases.Deployments.DeployStack;

public class DeployStackHandler : IRequestHandler<DeployStackCommand, DeployStackResponse>
{
    private readonly IProductSourceService _productSourceService;
    private readonly IDeploymentService _deploymentService;
    private readonly IDeploymentNotificationService? _notificationService;
    private readonly ILogger<DeployStackHandler> _logger;
    private readonly TimeProvider _timeProvider;

    public DeployStackHandler(
        IProductSourceService productSourceService,
        IDeploymentService deploymentService,
        ILogger<DeployStackHandler> logger,
        IDeploymentNotificationService? notificationService = null,
        TimeProvider? timeProvider = null)
    {
        _productSourceService = productSourceService;
        _deploymentService = deploymentService;
        _logger = logger;
        _notificationService = notificationService;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
        var observerConfig = product?.MaintenanceObserver != null
            ? MapToDeploymentObserverConfig(product.MaintenanceObserver, request.Variables)
            : null;

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
            HealthCheckConfigs = healthCheckConfigs
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
    /// Maps RsgoMaintenanceObserver (StackManagement domain) to MaintenanceObserverConfig (Deployment domain).
    /// This is the boundary mapping between bounded contexts.
    /// </summary>
    private static MaintenanceObserverConfig? MapToDeploymentObserverConfig(
        RsgoMaintenanceObserver source,
        Dictionary<string, string> deploymentVariables)
    {
        // Parse observer type
        if (!ObserverType.TryFromValue(source.Type, out var observerType) || observerType == null)
        {
            return null;
        }

        // Parse polling interval
        var pollingInterval = ParseTimeSpan(source.PollingInterval) ?? TimeSpan.FromSeconds(30);

        // Create type-specific settings
        IObserverSettings settings;

        if (observerType == ObserverType.SqlExtendedProperty)
        {
            var connectionString = ResolveConnectionString(source, deploymentVariables);
            if (string.IsNullOrEmpty(connectionString))
                return null;

            settings = SqlObserverSettings.ForExtendedProperty(
                source.PropertyName ?? throw new InvalidOperationException("PropertyName required"),
                connectionString);
        }
        else if (observerType == ObserverType.SqlQuery)
        {
            var connectionString = ResolveConnectionString(source, deploymentVariables);
            if (string.IsNullOrEmpty(connectionString))
                return null;

            settings = SqlObserverSettings.ForQuery(
                source.Query ?? throw new InvalidOperationException("Query required"),
                connectionString);
        }
        else if (observerType == ObserverType.Http)
        {
            var timeout = ParseTimeSpan(source.Timeout) ?? TimeSpan.FromSeconds(10);
            settings = HttpObserverSettings.Create(
                source.Url ?? throw new InvalidOperationException("URL required"),
                source.Method ?? "GET",
                null, // Headers not in RsgoMaintenanceObserver
                timeout,
                source.JsonPath);
        }
        else if (observerType == ObserverType.File)
        {
            var mode = source.Mode?.ToLowerInvariant() == "content"
                ? FileCheckMode.Content
                : FileCheckMode.Exists;

            settings = mode == FileCheckMode.Content
                ? FileObserverSettings.ForContent(
                    source.Path ?? throw new InvalidOperationException("Path required"),
                    source.ContentPattern)
                : FileObserverSettings.ForExistence(
                    source.Path ?? throw new InvalidOperationException("Path required"));
        }
        else
        {
            return null;
        }

        return MaintenanceObserverConfig.Create(
            observerType,
            pollingInterval,
            source.MaintenanceValue,
            source.NormalValue,
            settings);
    }

    /// <summary>
    /// Resolves connection string from direct value or variable reference.
    /// </summary>
    private static string? ResolveConnectionString(
        RsgoMaintenanceObserver source,
        Dictionary<string, string> variables)
    {
        // Direct connection string - resolve variables if present
        if (!string.IsNullOrEmpty(source.ConnectionString))
        {
            return ResolveVariables(source.ConnectionString, variables);
        }

        // Connection name - look up in deployment variables
        if (!string.IsNullOrEmpty(source.ConnectionName) &&
            variables.TryGetValue(source.ConnectionName, out var connectionString))
        {
            return connectionString;
        }

        return null;
    }

    /// <summary>
    /// Resolves ${VAR_NAME} placeholders in a template string.
    /// </summary>
    private static string? ResolveVariables(string template, Dictionary<string, string> variables)
    {
        if (string.IsNullOrEmpty(template))
            return template;

        var result = template;
        foreach (var kvp in variables)
        {
            result = result.Replace($"${{{kvp.Key}}}", kvp.Value);
        }

        // Check if any unresolved placeholders remain
        if (result.Contains("${"))
            return null;

        return result;
    }

    /// <summary>
    /// Parses time span strings like "30s", "1m", "5m", "1h".
    /// </summary>
    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        value = value.Trim().ToLowerInvariant();

        if (value.EndsWith('s') && int.TryParse(value[..^1], out var seconds))
            return TimeSpan.FromSeconds(seconds);

        if (value.EndsWith('m') && int.TryParse(value[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);

        if (value.EndsWith('h') && int.TryParse(value[..^1], out var hours))
            return TimeSpan.FromHours(hours);

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
