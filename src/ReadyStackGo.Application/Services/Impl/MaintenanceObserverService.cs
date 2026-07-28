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
/// Uses observer configuration stored on ProductDeployment entities (one check per product).
/// Observer instances, results and check timestamps live in <see cref="IMaintenanceObserverStateStore"/>
/// because this service is scoped and re-created for every check cycle.
/// </summary>
public class MaintenanceObserverService : IMaintenanceObserverService
{
    private readonly IMaintenanceObserverFactory _observerFactory;
    private readonly IMaintenanceObserverStateStore _state;
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IHealthSnapshotRepository _healthSnapshotRepository;
    private readonly IHealthNotificationService _notificationService;
    private readonly ISender _mediator;
    private readonly ILogger<MaintenanceObserverService> _logger;

    public MaintenanceObserverService(
        IMaintenanceObserverFactory observerFactory,
        IMaintenanceObserverStateStore state,
        IProductDeploymentRepository productDeploymentRepository,
        IHealthSnapshotRepository healthSnapshotRepository,
        IHealthNotificationService notificationService,
        ISender mediator,
        ILogger<MaintenanceObserverService> logger)
    {
        _observerFactory = observerFactory;
        _state = state;
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
            _state.Forget(productDeploymentId);
            return null;
        }

        // Only check operational product deployments
        if (!productDeployment.IsOperational)
        {
            return null;
        }

        var observerConfig = productDeployment.MaintenanceObserverConfig;
        if (observerConfig == null)
        {
            _logger.LogDebug("No maintenance observer configured for product {ProductName}",
                productDeployment.ProductName);
            _state.Forget(productDeploymentId);
            return null;
        }

        if (IsSkippedForManualMaintenance(productDeployment))
        {
            return _state.GetLastResult(productDeploymentId);
        }

        // Check if enough time has passed since last check (respecting polling interval)
        if (!_state.ShouldCheck(productDeploymentId, observerConfig.PollingInterval))
        {
            return _state.GetLastResult(productDeploymentId);
        }

        var observer = _state.GetOrCreateObserver(productDeploymentId, observerConfig, config =>
        {
            var created = _observerFactory.Create(config);

            _logger.LogInformation(
                "Created maintenance observer for product {ProductName}: type={ObserverType}, interval={Interval}s",
                productDeployment.ProductName, created.Type.DisplayName, config.PollingInterval.TotalSeconds);

            return created;
        });

        // Perform the check
        var result = await observer.CheckAsync(cancellationToken);

        _state.RecordResult(productDeploymentId, result);

        // Handle result
        await HandleObserverResultAsync(productDeployment, observerConfig, result, cancellationToken);

        return result;
    }

    public Task<ObserverResult?> GetLastResultAsync(ProductDeploymentId productDeploymentId)
        => Task.FromResult(_state.GetLastResult(productDeploymentId));

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

    /// <summary>
    /// Manually activated maintenance is owned by the operator: the observer is not allowed to end it
    /// (see <see cref="HandleObserverResultAsync"/>), so a check could not act on its own result.
    /// Skipping it entirely means RSGO opens no connection to the product at all while an operator
    /// works on it — a product maintenance routine that waits for every session to close before
    /// taking its database exclusively must never end up waiting on RSGO.
    /// </summary>
    private bool IsSkippedForManualMaintenance(ProductDeployment productDeployment)
    {
        if (productDeployment.OperationMode != OperationMode.Maintenance ||
            productDeployment.MaintenanceTrigger?.IsManual != true)
        {
            return false;
        }

        _logger.LogDebug(
            "Skipping maintenance observer check for product {ProductName}: maintenance was activated " +
            "manually, so the observer cannot act on the result and stays off the product",
            productDeployment.ProductName);

        return true;
    }

    private async Task HandleObserverResultAsync(
        ProductDeployment productDeployment,
        MaintenanceObserverConfig observerConfig,
        ObserverResult result,
        CancellationToken cancellationToken)
    {
        var observerType = observerConfig.Type.Value;

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
            // Logged at Information level with the observed value so a blocked product maintenance
            // window can be verified in the field: RSGO reacted, and its own connections are
            // non-pooled, so no RSGO session remains on the product database once containers stop.
            _logger.LogInformation(
                "Maintenance observer triggered maintenance mode for product {ProductName} (observed: {Value}); " +
                "stopping containers, RSGO keeps no pooled session on the product",
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
