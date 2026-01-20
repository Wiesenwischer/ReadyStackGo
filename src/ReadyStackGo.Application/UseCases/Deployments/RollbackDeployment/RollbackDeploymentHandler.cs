using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.StackManagement.Manifests;
using RuntimeConfig = ReadyStackGo.Domain.Deployment.RuntimeConfig;

namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

/// <summary>
/// Handler for rolling back a deployment after a failed upgrade.
/// Rollback simply redeploys the current version using existing deployment data
/// (StackId, StackVersion, Variables are all preserved in the Deployment entity).
/// </summary>
public class RollbackDeploymentHandler : IRequestHandler<RollbackDeploymentCommand, RollbackDeploymentResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IProductSourceService _productSourceService;
    private readonly IDeploymentService _deploymentService;
    private readonly ILogger<RollbackDeploymentHandler> _logger;

    public RollbackDeploymentHandler(
        IDeploymentRepository deploymentRepository,
        IProductSourceService productSourceService,
        IDeploymentService deploymentService,
        ILogger<RollbackDeploymentHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _productSourceService = productSourceService;
        _deploymentService = deploymentService;
        _logger = logger;
    }

    public async Task<RollbackDeploymentResponse> Handle(RollbackDeploymentCommand request, CancellationToken cancellationToken)
    {
        // Validate deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Invalid deployment ID format."
            };
        }

        // Validate environment ID
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Invalid environment ID format."
            };
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);
        var deployment = _deploymentRepository.GetById(deploymentId);

        if (deployment == null)
        {
            return new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Deployment not found."
            };
        }

        // Verify environment access
        var environmentId = new EnvironmentId(envGuid);
        if (deployment.EnvironmentId != environmentId)
        {
            return new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Deployment not found in this environment."
            };
        }

        // Check if rollback is possible
        if (!deployment.CanRollback())
        {
            return new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Rollback only available after failed upgrade."
            };
        }

        // Get deployment data for rollback - no snapshot needed, deployment has all the info
        var (stackId, targetVersion, variables) = deployment.GetRedeploymentData();

        _logger.LogInformation("Rolling back deployment {DeploymentId} to version {Version}",
            request.DeploymentId, targetVersion);

        try
        {
            // Get the stack definition from catalog
            var stackDefinition = await _productSourceService.GetStackAsync(stackId, cancellationToken);
            if (stackDefinition == null)
            {
                return new RollbackDeploymentResponse
                {
                    Success = false,
                    Message = $"Stack '{stackId}' not found in catalog. Cannot complete rollback."
                };
            }

            // Get the product to access MaintenanceObserver
            var productId = GetProductIdFromStackId(stackId);
            var product = productId != null
                ? await _productSourceService.GetProductAsync(productId, cancellationToken)
                : null;

            // Map MaintenanceObserver from StackManagement to Deployment domain model
            var variablesDict = variables.ToDictionary(kv => kv.Key, kv => kv.Value);
            var observerConfig = product?.MaintenanceObserver != null
                ? MapToDeploymentObserverConfig(product.MaintenanceObserver, variablesDict)
                : null;

            // Extract health check configurations from services
            var healthCheckConfigs = ExtractHealthCheckConfigs(stackDefinition.Services);

            // Create deploy request using existing deployment data
            var deployRequest = new DeployStackRequest
            {
                StackName = deployment.StackName,
                Services = stackDefinition.Services,
                Volumes = stackDefinition.Volumes,
                Networks = stackDefinition.Networks,
                StackVersion = targetVersion,
                Variables = variablesDict,
                EnvironmentId = request.EnvironmentId,
                CatalogStackId = stackId,
                MaintenanceObserver = observerConfig,
                HealthCheckConfigs = healthCheckConfigs
            };

            // Deploy containers with existing configuration
            var result = await _deploymentService.DeployStackAsync(
                request.EnvironmentId,
                deployRequest,
                null, // No progress callback for rollback (could be added later)
                cancellationToken);

            if (!result.Success)
            {
                _logger.LogError("Rollback deployment failed for {DeploymentId}: {Errors}",
                    request.DeploymentId, string.Join("; ", result.Errors));

                return new RollbackDeploymentResponse
                {
                    Success = false,
                    Message = $"Rollback failed: {result.Message}",
                    DeploymentId = request.DeploymentId,
                    TargetVersion = targetVersion
                };
            }

            _logger.LogInformation("Rollback completed successfully for {DeploymentId} to version {Version}",
                request.DeploymentId, targetVersion);

            return new RollbackDeploymentResponse
            {
                Success = true,
                Message = $"Successfully rolled back to version {targetVersion}.",
                DeploymentId = request.DeploymentId,
                TargetVersion = targetVersion
            };
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Rollback operation failed for {DeploymentId}", request.DeploymentId);
            return new RollbackDeploymentResponse
            {
                Success = false,
                Message = ex.Message
            };
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
    /// Maps RsgoMaintenanceObserver (StackManagement domain) to MaintenanceObserverConfig (Deployment domain).
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
    /// </summary>
    private static IReadOnlyList<RuntimeConfig.ServiceHealthCheckConfig>? ExtractHealthCheckConfigs(
        IReadOnlyList<Domain.StackManagement.Stacks.ServiceTemplate> services)
    {
        var configs = new List<RuntimeConfig.ServiceHealthCheckConfig>();

        foreach (var service in services)
        {
            if (service.HealthCheck == null)
                continue;

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
