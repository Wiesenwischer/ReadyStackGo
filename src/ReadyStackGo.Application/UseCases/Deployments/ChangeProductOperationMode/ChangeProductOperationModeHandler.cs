namespace ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;
using ReadyStackGo.Domain.Deployment.Observers;
using ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Handler for changing the operation mode of a product deployment.
/// Entering maintenance stops containers of ALL child stacks and propagates
/// the maintenance mode to each child Deployment aggregate.
/// Exiting maintenance starts containers and resets child Deployments to Normal mode.
/// </summary>
public class ChangeProductOperationModeHandler
    : IRequestHandler<ChangeProductOperationModeCommand, ChangeProductOperationModeResponse>
{
    private readonly IProductDeploymentRepository _productDeploymentRepository;
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IDockerService _dockerService;
    private readonly IHealthNotificationService _healthNotificationService;
    private readonly ILogger<ChangeProductOperationModeHandler> _logger;

    public ChangeProductOperationModeHandler(
        IProductDeploymentRepository productDeploymentRepository,
        IDeploymentRepository deploymentRepository,
        IDockerService dockerService,
        IHealthNotificationService healthNotificationService,
        ILogger<ChangeProductOperationModeHandler> logger)
    {
        _productDeploymentRepository = productDeploymentRepository;
        _deploymentRepository = deploymentRepository;
        _dockerService = dockerService;
        _healthNotificationService = healthNotificationService;
        _logger = logger;
    }

    public async Task<ChangeProductOperationModeResponse> Handle(
        ChangeProductOperationModeCommand request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.ProductDeploymentId, out var productDeploymentGuid))
        {
            return ChangeProductOperationModeResponse.Fail("Invalid product deployment ID format");
        }

        var productDeploymentId = new ProductDeploymentId(productDeploymentGuid);
        var productDeployment = _productDeploymentRepository.Get(productDeploymentId);

        if (productDeployment == null)
        {
            return ChangeProductOperationModeResponse.Fail("Product deployment not found");
        }

        if (!productDeployment.IsOperational)
        {
            return ChangeProductOperationModeResponse.Fail(
                $"Cannot change operation mode. Product deployment is {productDeployment.Status}, must be Running or PartiallyRunning.");
        }

        if (!OperationMode.TryFromName(request.NewMode, out var targetMode) || targetMode == null)
        {
            var validModes = string.Join(", ", OperationMode.GetAll().Select(m => m.Name));
            return ChangeProductOperationModeResponse.Fail(
                $"Invalid operation mode '{request.NewMode}'. Valid modes: {validModes}");
        }

        var previousMode = productDeployment.OperationMode;

        if (previousMode == targetMode)
        {
            return ChangeProductOperationModeResponse.Ok(
                request.ProductDeploymentId, previousMode.Name, targetMode.Name);
        }

        var source = string.Equals(request.Source, "Observer", StringComparison.OrdinalIgnoreCase)
            ? MaintenanceTriggerSource.Observer
            : MaintenanceTriggerSource.Manual;

        MaintenanceTrigger? trigger = null;

        try
        {
            if (targetMode == OperationMode.Maintenance)
            {
                trigger = source == MaintenanceTriggerSource.Observer
                    ? MaintenanceTrigger.Observer(request.Reason)
                    : MaintenanceTrigger.Manual(request.Reason);
                productDeployment.EnterMaintenance(trigger);
            }
            else if (targetMode == OperationMode.Normal)
            {
                productDeployment.ExitMaintenance(source);
            }
            else
            {
                return ChangeProductOperationModeResponse.Fail($"Unknown target mode: {targetMode.Name}");
            }
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
        {
            _logger.LogWarning(ex, "Failed to change operation mode for product deployment {ProductDeploymentId}",
                request.ProductDeploymentId);
            return ChangeProductOperationModeResponse.Fail(ex.Message);
        }

        _productDeploymentRepository.SaveChanges();

        _logger.LogInformation(
            "Changed operation mode for product deployment {ProductDeploymentId} ({ProductName}) from {PreviousMode} to {NewMode}",
            request.ProductDeploymentId, productDeployment.ProductName, previousMode.Name, targetMode.Name);

        // Handle container lifecycle and propagate operation mode to ALL child stacks
        await HandleContainerLifecycleAsync(
            productDeployment, previousMode, targetMode, trigger, source, cancellationToken);

        return ChangeProductOperationModeResponse.Ok(
            request.ProductDeploymentId, previousMode.Name, targetMode.Name, source.ToString());
    }

    private async Task HandleContainerLifecycleAsync(
        ProductDeployment productDeployment,
        OperationMode previousMode,
        OperationMode targetMode,
        MaintenanceTrigger? trigger,
        MaintenanceTriggerSource source,
        CancellationToken cancellationToken)
    {
        var environmentId = productDeployment.EnvironmentId.Value.ToString();

        foreach (var stack in productDeployment.Stacks)
        {
            if (stack.Status != StackDeploymentStatus.Running) continue;

            try
            {
                // Propagate operation mode to child Deployment aggregate
                PropagateOperationModeToChildDeployment(stack, targetMode, trigger, source);

                if (targetMode == OperationMode.Maintenance)
                {
                    _logger.LogInformation(
                        "Stopping containers for stack {StackName} (product maintenance)",
                        stack.StackName);

                    await _dockerService.StopStackContainersAsync(
                        environmentId, stack.DeploymentStackName!, cancellationToken);
                }
                else if (previousMode == OperationMode.Maintenance && targetMode == OperationMode.Normal)
                {
                    _logger.LogInformation(
                        "Starting containers for stack {StackName} (product maintenance exit)",
                        stack.StackName);

                    await _dockerService.StartStackContainersAsync(
                        environmentId, stack.DeploymentStackName!, cancellationToken);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to manage containers for stack {StackName} during product mode transition",
                    stack.StackName);
            }
        }

        _deploymentRepository.SaveChanges();
    }

    private void PropagateOperationModeToChildDeployment(
        ProductStackDeployment stack,
        OperationMode targetMode,
        MaintenanceTrigger? trigger,
        MaintenanceTriggerSource source)
    {
        if (stack.DeploymentId == null) return;

        var deployment = _deploymentRepository.Get(stack.DeploymentId);
        if (deployment == null)
        {
            _logger.LogWarning(
                "Child deployment {DeploymentId} not found for stack {StackName}, skipping operation mode propagation",
                stack.DeploymentId, stack.StackName);
            return;
        }

        try
        {
            if (targetMode == OperationMode.Maintenance && deployment.OperationMode == OperationMode.Normal)
            {
                deployment.EnterMaintenance(trigger!);
            }
            else if (targetMode == OperationMode.Normal && deployment.OperationMode == OperationMode.Maintenance)
            {
                deployment.ExitMaintenance(source);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Failed to propagate operation mode to child deployment {DeploymentId} for stack {StackName}",
                stack.DeploymentId, stack.StackName);
        }
    }
}
