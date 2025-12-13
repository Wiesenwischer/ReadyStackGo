using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Service that coordinates maintenance observer checks across all deployments.
/// Caches observer instances and last results per deployment.
/// Uses observer configuration stored directly on Deployment entities.
/// </summary>
public class MaintenanceObserverService : IMaintenanceObserverService
{
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
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
        IHealthNotificationService notificationService,
        ISender mediator,
        ILogger<MaintenanceObserverService> logger)
    {
        _observerFactory = observerFactory;
        _deploymentRepository = deploymentRepository;
        _healthSnapshotRepository = healthSnapshotRepository;
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

    private Task<IMaintenanceObserver?> GetOrCreateObserverAsync(Deployment deployment)
    {
        // Try to get cached observer
        if (_observers.TryGetValue(deployment.Id.Value, out var cachedObserver))
        {
            return Task.FromResult<IMaintenanceObserver?>(cachedObserver);
        }

        // Get observer configuration from the deployment entity (set at deploy time)
        var observerConfig = deployment.MaintenanceObserverConfig;
        if (observerConfig == null)
        {
            _logger.LogDebug("No maintenance observer configured for deployment {StackName}", deployment.StackName);
            return Task.FromResult<IMaintenanceObserver?>(null);
        }

        // Create observer and cache it
        var observer = _observerFactory.Create(observerConfig);
        _observers[deployment.Id.Value] = observer;
        _configs[deployment.Id.Value] = observerConfig;

        _logger.LogInformation(
            "Created maintenance observer for deployment {StackName}: type={ObserverType}",
            deployment.StackName, observer.Type.DisplayName);

        return Task.FromResult<IMaintenanceObserver?>(observer);
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
        // Get observer type for notification
        var observerType = _configs.TryGetValue(deployment.Id.Value, out var config)
            ? config.Type.Value
            : null;

        // Always notify clients about observer results (success or failure)
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
}
