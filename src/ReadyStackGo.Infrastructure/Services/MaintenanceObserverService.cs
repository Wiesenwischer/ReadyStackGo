using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Service that coordinates maintenance observer checks across all deployments.
/// Caches observer instances and last results per deployment.
/// </summary>
public class MaintenanceObserverService : IMaintenanceObserverService
{
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IStackSourceService _stackSourceService;
    private readonly IRsgoManifestParser _manifestParser;
    private readonly IHealthNotificationService _notificationService;
    private readonly ISender _mediator;
    private readonly ILogger<MaintenanceObserverService> _logger;

    // Cache for observer instances per deployment
    private readonly ConcurrentDictionary<Guid, IMaintenanceObserver> _observers = new();

    // Cache for last results per deployment
    private readonly ConcurrentDictionary<Guid, ObserverResult> _lastResults = new();

    // Track last check time per deployment to respect individual polling intervals
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastCheckTimes = new();

    // Cache for observer configs per deployment
    private readonly ConcurrentDictionary<Guid, MaintenanceObserverConfig> _configs = new();

    public MaintenanceObserverService(
        IMaintenanceObserverFactory observerFactory,
        IDeploymentRepository deploymentRepository,
        IHealthSnapshotRepository healthSnapshotRepository,
        IStackSourceService stackSourceService,
        IRsgoManifestParser manifestParser,
        IHealthNotificationService notificationService,
        ISender mediator,
        ILogger<MaintenanceObserverService> logger)
    {
        _observerFactory = observerFactory;
        _deploymentRepository = deploymentRepository;
        _healthSnapshotRepository = healthSnapshotRepository;
        _stackSourceService = stackSourceService;
        _manifestParser = manifestParser;
        _notificationService = notificationService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task CheckAllObserversAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting maintenance observer check cycle");

        var activeDeployments = _deploymentRepository.GetAllActive().ToList();

        foreach (var deployment in activeDeployments)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await CheckDeploymentObserverAsync(deployment.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking maintenance observer for deployment {DeploymentId}", deployment.Id);
            }
        }

        _logger.LogDebug("Maintenance observer check cycle completed");
    }

    public async Task<ObserverResult?> CheckDeploymentObserverAsync(
        DeploymentId deploymentId,
        CancellationToken cancellationToken = default)
    {
        var deployment = _deploymentRepository.Get(deploymentId);
        if (deployment == null)
        {
            _logger.LogDebug("Deployment {DeploymentId} not found", deploymentId);
            return null;
        }

        // Only check running deployments
        if (deployment.Status != DeploymentStatus.Running)
        {
            return null;
        }

        // Get or create observer for this deployment
        var observer = await GetOrCreateObserverAsync(deployment);
        if (observer == null)
        {
            // No observer configured for this deployment
            return null;
        }

        // Check if enough time has passed since last check (respecting polling interval)
        if (!ShouldCheck(deploymentId.Value))
        {
            return _lastResults.GetValueOrDefault(deploymentId.Value);
        }

        // Perform the check
        var result = await observer.CheckAsync(cancellationToken);

        // Update caches
        _lastResults[deploymentId.Value] = result;
        _lastCheckTimes[deploymentId.Value] = DateTimeOffset.UtcNow;

        // Handle result - log and track for now
        // Full integration with OperationMode change will be added later
        await HandleObserverResultAsync(deployment, result, cancellationToken);

        return result;
    }

    public Task<ObserverResult?> GetLastResultAsync(DeploymentId deploymentId)
    {
        var result = _lastResults.GetValueOrDefault(deploymentId.Value);
        return Task.FromResult(result);
    }

    private async Task<IMaintenanceObserver?> GetOrCreateObserverAsync(Deployment deployment)
    {
        // Try to get cached observer
        if (_observers.TryGetValue(deployment.Id.Value, out var cachedObserver))
        {
            return cachedObserver;
        }

        // Load manifest and check for observer configuration
        var observerConfig = await LoadObserverConfigAsync(deployment);
        if (observerConfig == null)
        {
            return null;
        }

        // Create observer and cache it
        var observer = _observerFactory.Create(observerConfig);
        _observers[deployment.Id.Value] = observer;
        _configs[deployment.Id.Value] = observerConfig;

        _logger.LogInformation(
            "Created maintenance observer for deployment {StackName}: type={ObserverType}",
            deployment.StackName, observer.Type.DisplayName);

        return observer;
    }

    private async Task<MaintenanceObserverConfig?> LoadObserverConfigAsync(Deployment deployment)
    {
        try
        {
            // Get all stacks and find the one matching this deployment
            var stacks = await _stackSourceService.GetStacksAsync();
            var stack = stacks.FirstOrDefault(s =>
                s.Name.Equals(deployment.StackName, StringComparison.OrdinalIgnoreCase));

            if (stack == null)
            {
                _logger.LogDebug("Stack definition not found for {StackName}", deployment.StackName);
                return null;
            }

            // Parse the YAML content to get the full manifest with maintenanceObserver
            var manifest = await _manifestParser.ParseAsync(stack.YamlContent);

            if (manifest?.MaintenanceObserver == null)
            {
                return null;
            }

            var manifestObserver = manifest.MaintenanceObserver;
            return ConvertToConfig(manifestObserver);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load observer config for deployment {DeploymentId}", deployment.Id);
            return null;
        }
    }

    private MaintenanceObserverConfig? ConvertToConfig(RsgoMaintenanceObserver manifestObserver)
    {
        if (!ObserverType.TryFromValue(manifestObserver.Type, out var observerType) || observerType == null)
        {
            _logger.LogWarning("Unknown observer type: {Type}", manifestObserver.Type);
            return null;
        }

        var pollingInterval = ParseTimeSpan(manifestObserver.PollingInterval) ?? TimeSpan.FromSeconds(30);

        IObserverSettings settings;

        if (observerType == ObserverType.SqlExtendedProperty)
        {
            var connectionString = ResolveConnectionString(manifestObserver);
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("No connection string available for SQL observer");
                return null;
            }
            settings = SqlObserverSettings.ForExtendedProperty(
                manifestObserver.PropertyName ?? throw new InvalidOperationException("PropertyName required"),
                connectionString);
        }
        else if (observerType == ObserverType.SqlQuery)
        {
            var connectionString = ResolveConnectionString(manifestObserver);
            if (string.IsNullOrEmpty(connectionString))
            {
                _logger.LogWarning("No connection string available for SQL observer");
                return null;
            }
            settings = SqlObserverSettings.ForQuery(
                manifestObserver.Query ?? throw new InvalidOperationException("Query required"),
                connectionString);
        }
        else if (observerType == ObserverType.Http)
        {
            var timeout = ParseTimeSpan(manifestObserver.Timeout) ?? TimeSpan.FromSeconds(10);
            settings = HttpObserverSettings.Create(
                manifestObserver.Url ?? throw new InvalidOperationException("URL required"),
                manifestObserver.Method ?? "GET",
                manifestObserver.Headers,
                timeout,
                manifestObserver.JsonPath);
        }
        else if (observerType == ObserverType.File)
        {
            var mode = manifestObserver.Mode?.ToLowerInvariant() == "content"
                ? FileCheckMode.Content
                : FileCheckMode.Exists;

            settings = mode == FileCheckMode.Content
                ? FileObserverSettings.ForContent(
                    manifestObserver.Path ?? throw new InvalidOperationException("Path required"),
                    manifestObserver.ContentPattern)
                : FileObserverSettings.ForExistence(
                    manifestObserver.Path ?? throw new InvalidOperationException("Path required"));
        }
        else
        {
            _logger.LogWarning("Unsupported observer type: {Type}", observerType.Value);
            return null;
        }

        return MaintenanceObserverConfig.Create(
            observerType,
            pollingInterval,
            manifestObserver.MaintenanceValue,
            manifestObserver.NormalValue,
            settings);
    }

    private string? ResolveConnectionString(RsgoMaintenanceObserver manifestObserver)
    {
        // Direct connection string - return as-is (variable resolution happens elsewhere)
        if (!string.IsNullOrEmpty(manifestObserver.ConnectionString))
        {
            return manifestObserver.ConnectionString;
        }

        // Connection name - not implemented yet, needs variable resolver
        if (!string.IsNullOrEmpty(manifestObserver.ConnectionName))
        {
            _logger.LogWarning(
                "ConnectionName '{ConnectionName}' - variable resolution not yet implemented",
                manifestObserver.ConnectionName);
        }

        return null;
    }

    private bool ShouldCheck(Guid deploymentId)
    {
        if (!_lastCheckTimes.TryGetValue(deploymentId, out var lastCheck))
        {
            return true;
        }

        // Get polling interval from config if available
        var interval = TimeSpan.FromSeconds(30);
        if (_configs.TryGetValue(deploymentId, out var config))
        {
            interval = config.PollingInterval;
        }

        return DateTimeOffset.UtcNow - lastCheck >= interval;
    }

    private async Task HandleObserverResultAsync(
        Deployment deployment,
        ObserverResult result,
        CancellationToken cancellationToken)
    {
        // Get observer type for the DTO
        string? observerType = null;
        if (_configs.TryGetValue(deployment.Id.Value, out var config))
        {
            observerType = config.Type.Value;
        }

        // Send SignalR notification
        var resultDto = ObserverResultDto.FromDomain(result, observerType);
        await _notificationService.NotifyObserverResultAsync(
            deployment.Id,
            deployment.StackName,
            resultDto,
            cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Maintenance observer check failed for {StackName}: {Error}",
                deployment.StackName, result.ErrorMessage);
            return;
        }

        // Get current operation mode from deployment (OperationMode is now on Deployment aggregate)
        var currentMode = deployment.OperationMode;
        var shouldBeMaintenance = result.IsMaintenanceRequired;

        // Handle mode transitions via the business logic (ChangeOperationModeCommand)
        if (shouldBeMaintenance && currentMode != OperationMode.Maintenance)
        {
            _logger.LogInformation(
                "Maintenance observer triggered maintenance mode for {StackName} (observed: {Value})",
                deployment.StackName, result.ObservedValue);

            // Use ChangeOperationModeCommand to properly enter maintenance mode
            // This triggers the same business logic as the UI button
            var command = new ChangeOperationModeCommand(
                deployment.Id.Value.ToString(),
                OperationMode.Maintenance.Name,
                Reason: $"Triggered by maintenance observer (observed: {result.ObservedValue})");

            var response = await _mediator.Send(command, cancellationToken);
            if (!response.Success)
            {
                _logger.LogWarning(
                    "Failed to enter maintenance mode for {StackName}: {Message}",
                    deployment.StackName, response.Message);
            }
        }
        else if (!shouldBeMaintenance && currentMode == OperationMode.Maintenance)
        {
            _logger.LogInformation(
                "Maintenance observer cleared maintenance mode for {StackName} (observed: {Value})",
                deployment.StackName, result.ObservedValue);

            // Use ChangeOperationModeCommand to properly exit maintenance mode
            var command = new ChangeOperationModeCommand(
                deployment.Id.Value.ToString(),
                OperationMode.Normal.Name,
                Reason: $"Cleared by maintenance observer (observed: {result.ObservedValue})");

            var response = await _mediator.Send(command, cancellationToken);
            if (!response.Success)
            {
                _logger.LogWarning(
                    "Failed to exit maintenance mode for {StackName}: {Message}",
                    deployment.StackName, response.Message);
            }
        }
    }

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return null;

        // Parse formats like "30s", "1m", "5m", "1h"
        value = value.Trim().ToLowerInvariant();

        if (value.EndsWith('s') && int.TryParse(value[..^1], out var seconds))
            return TimeSpan.FromSeconds(seconds);

        if (value.EndsWith('m') && int.TryParse(value[..^1], out var minutes))
            return TimeSpan.FromMinutes(minutes);

        if (value.EndsWith('h') && int.TryParse(value[..^1], out var hours))
            return TimeSpan.FromHours(hours);

        return null;
    }
}
