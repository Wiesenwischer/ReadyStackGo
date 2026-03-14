using System.Collections.Concurrent;
using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Service that coordinates maintenance observer checks across all product deployments.
/// Caches observer instances and last results per product deployment.
/// Uses observer configuration stored on ProductDeployment entities (one check per product).
/// </summary>
public class MaintenanceObserverService : IMaintenanceObserverService
{
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IHealthNotificationService _notificationService;
    private readonly ISender _mediator;
    private readonly ILogger<MaintenanceObserverService> _logger;

    // Cache for observer instances per product deployment
    private readonly ConcurrentDictionary<Guid, IMaintenanceObserver> _observers = new();

    // Cache for last results per product deployment
    private readonly ConcurrentDictionary<Guid, ObserverResult> _lastResults = new();

    // Track last check time per product deployment to respect individual polling intervals
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _lastCheckTimes = new();

    // Cache for observer configs per product deployment
    private readonly ConcurrentDictionary<Guid, MaintenanceObserverConfig> _configs = new();

    public MaintenanceObserverService(
        IMaintenanceObserverFactory observerFactory,
        IProductDeploymentRepository productDeploymentRepository,
        IHealthSnapshotRepository healthSnapshotRepository,
        IHealthNotificationService notificationService,
        ISender mediator,
        ILogger<MaintenanceObserverService> logger)
    {
        _observerFactory = observerFactory;
        _productDeploymentRepository = productDeploymentRepository;
        _healthSnapshotRepository = healthSnapshotRepository;
        _notificationService = notificationService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task CheckAllObserversAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Starting maintenance observer check cycle");

        var activeProductDeployments = _productDeploymentRepository.GetAllActive().ToList();

        foreach (var productDeployment in activeProductDeployments)
        {
            if (cancellationToken.IsCancellationRequested)
                break;

            try
            {
                await CheckProductObserverAsync(productDeployment.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Error checking maintenance observer for product deployment {ProductDeploymentId} ({ProductName})",
                    productDeployment.Id, productDeployment.ProductName);
            }
        }

        _logger.LogDebug("Maintenance observer check cycle completed");
    }

    public async Task<ObserverResult?> CheckProductObserverAsync(
        ProductDeploymentId productDeploymentId,
        CancellationToken cancellationToken = default)
    {
        var productDeployment = _productDeploymentRepository.Get(productDeploymentId);
        if (productDeployment == null)
        {
            _logger.LogDebug("Product deployment {ProductDeploymentId} not found", productDeploymentId);
            return null;
        }

        // Only check operational product deployments
        if (!productDeployment.IsOperational)
        {
            return null;
        }

        // Get or create observer for this product deployment
        var observer = GetOrCreateObserver(productDeployment);
        if (observer == null)
        {
            return null;
        }

        // Check if enough time has passed since last check (respecting polling interval)
        if (!ShouldCheck(productDeploymentId.Value))
        {
            return _lastResults.GetValueOrDefault(productDeploymentId.Value);
        }

        // Perform the check
        var result = await observer.CheckAsync(cancellationToken);

        // Update caches
        _lastResults[productDeploymentId.Value] = result;
        _lastCheckTimes[productDeploymentId.Value] = DateTimeOffset.UtcNow;

        // Handle result
        await HandleObserverResultAsync(productDeployment, result, cancellationToken);

        return result;
    }

    public Task<ObserverResult?> GetLastResultAsync(ProductDeploymentId productDeploymentId)
    {
        var result = _lastResults.GetValueOrDefault(productDeploymentId.Value);
        return Task.FromResult(result);
    }

    public async Task<ObserverResult?> CheckDeploymentObserverAsync(
        DeploymentId deploymentId,
        CancellationToken cancellationToken = default)
    {
        // Look up parent product deployment for this stack
        var productDeployment = _productDeploymentRepository.GetByStackDeploymentId(deploymentId);
        if (productDeployment == null)
        {
            _logger.LogDebug("No parent product deployment found for deployment {DeploymentId}", deploymentId);
            return null;
        }

        return await CheckProductObserverAsync(productDeployment.Id, cancellationToken);
    }

    public Task<ObserverResult?> GetLastResultAsync(DeploymentId deploymentId)
    {
        // Look up parent product deployment for this stack
        var productDeployment = _productDeploymentRepository.GetByStackDeploymentId(deploymentId);
        if (productDeployment == null)
        {
            return Task.FromResult<ObserverResult?>(null);
        }

        return GetLastResultAsync(productDeployment.Id);
    }

    private IMaintenanceObserver? GetOrCreateObserver(ProductDeployment productDeployment)
    {
        var id = productDeployment.Id.Value;

        // Try to get cached observer
        if (_observers.TryGetValue(id, out var cachedObserver))
        {
            return cachedObserver;
        }

        // Get observer configuration from the product deployment
        var observerConfig = productDeployment.MaintenanceObserverConfig;
        if (observerConfig == null)
        {
            _logger.LogDebug("No maintenance observer configured for product {ProductName}",
                productDeployment.ProductName);
            return null;
        }

        // Create observer and cache it
        var observer = _observerFactory.Create(observerConfig);
        _observers[id] = observer;
        _configs[id] = observerConfig;

        _logger.LogInformation(
            "Created maintenance observer for product {ProductName}: type={ObserverType}",
            productDeployment.ProductName, observer.Type.DisplayName);

        return observer;
    }

    private bool ShouldCheck(Guid productDeploymentId)
    {
        if (!_lastCheckTimes.TryGetValue(productDeploymentId, out var lastCheck))
        {
            return true;
        }

        var interval = TimeSpan.FromSeconds(30);
        if (_configs.TryGetValue(productDeploymentId, out var config))
        {
            interval = config.PollingInterval;
        }

        return DateTimeOffset.UtcNow - lastCheck >= interval;
    }

    private async Task HandleObserverResultAsync(
        ProductDeployment productDeployment,
        ObserverResult result,
        CancellationToken cancellationToken)
    {
        var id = productDeployment.Id.Value;

        // Get observer type for notification
        var observerType = _configs.TryGetValue(id, out var config)
            ? config.Type.Value
            : null;

        // Notify clients about observer results for each running stack
        var resultDto = ObserverResultDto.FromDomain(result, observerType);
        foreach (var stack in productDeployment.Stacks.Where(s => s.Status == StackDeploymentStatus.Running))
        {
            if (stack.DeploymentId != null)
            {
                await _notificationService.NotifyObserverResultAsync(
                    stack.DeploymentId,
                    stack.StackName,
                    resultDto,
                    cancellationToken);
            }
        }

        if (!result.IsSuccess)
        {
            _logger.LogWarning(
                "Maintenance observer check failed for product {ProductName}: {Error}",
                productDeployment.ProductName, result.ErrorMessage);
            return;
        }

        var currentMode = productDeployment.OperationMode;
        var shouldBeMaintenance = result.IsMaintenanceRequired;
        var environmentId = productDeployment.EnvironmentId.Value.ToString();
        var productDeploymentId = productDeployment.Id.Value.ToString();

        // Handle mode transitions via ChangeProductOperationModeCommand
        if (shouldBeMaintenance && currentMode != OperationMode.Maintenance)
        {
            _logger.LogInformation(
                "Maintenance observer triggered maintenance mode for product {ProductName} (observed: {Value})",
                productDeployment.ProductName, result.ObservedValue);

            var command = new ChangeProductOperationModeCommand(
                environmentId,
                productDeploymentId,
                OperationMode.Maintenance.Name,
                Reason: $"Triggered by maintenance observer (observed: {result.ObservedValue})",
                Source: "Observer");

            var response = await _mediator.Send(command, cancellationToken);
            if (!response.Success)
            {
                _logger.LogWarning(
                    "Failed to enter maintenance mode for product {ProductName}: {Message}",
                    productDeployment.ProductName, response.Message);
            }
        }
        else if (!shouldBeMaintenance && currentMode == OperationMode.Maintenance)
        {
            // Observer can only exit maintenance it activated itself.
            if (productDeployment.MaintenanceTrigger?.IsManual == true)
            {
                _logger.LogDebug(
                    "Observer skipping exit for product {ProductName}: maintenance was manually activated",
                    productDeployment.ProductName);
                return;
            }

            _logger.LogInformation(
                "Maintenance observer cleared maintenance mode for product {ProductName} (observed: {Value})",
                productDeployment.ProductName, result.ObservedValue);

            var command = new ChangeProductOperationModeCommand(
                environmentId,
                productDeploymentId,
                OperationMode.Normal.Name,
                Reason: $"Cleared by maintenance observer (observed: {result.ObservedValue})",
                Source: "Observer");

            var response = await _mediator.Send(command, cancellationToken);
            if (!response.Success)
            {
                _logger.LogWarning(
                    "Failed to exit maintenance mode for product {ProductName}: {Message}",
                    productDeployment.ProductName, response.Message);
            }
        }
    }
}
