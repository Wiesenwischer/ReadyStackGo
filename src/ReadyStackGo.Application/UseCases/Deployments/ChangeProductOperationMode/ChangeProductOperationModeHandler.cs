namespace ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Containers;
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
    private readonly IMaintenanceSetterService _maintenanceSetterService;
    private readonly IDeploymentNotificationService _deploymentNotificationService;
    private readonly ILogger<ChangeProductOperationModeHandler> _logger;

    /// <summary>
    /// Stack statuses whose containers can still be up, and which a maintenance transition therefore
    /// has to handle. <c>Pending</c> was never deployed, <c>Removed</c> is gone and <c>Stopped</c>
    /// was deliberately stopped already — none of them own live containers.
    /// </summary>
    private static readonly StackDeploymentStatus[] MaintenanceRelevantStatuses =
    [
        StackDeploymentStatus.Running,
        StackDeploymentStatus.Deploying,
        StackDeploymentStatus.Failed
    ];

    public ChangeProductOperationModeHandler(
        IProductDeploymentRepository productDeploymentRepository,
        IDeploymentRepository deploymentRepository,
        IDockerService dockerService,
        IHealthNotificationService healthNotificationService,
        IMaintenanceSetterService maintenanceSetterService,
        IDeploymentNotificationService deploymentNotificationService,
        ILogger<ChangeProductOperationModeHandler> logger)
    {
        _productDeploymentRepository = productDeploymentRepository;
        _deploymentRepository = deploymentRepository;
        _dockerService = dockerService;
        _healthNotificationService = healthNotificationService;
        _maintenanceSetterService = maintenanceSetterService;
        _deploymentNotificationService = deploymentNotificationService;
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

        // Propagate the state to the product via the maintenance setter (best-effort).
        // Loop protection: only fire for Manual transitions. Observer-initiated transitions
        // mean the product already set the flag itself — re-writing would be redundant and
        // could drive a feedback loop.
        var fireSetter = source == MaintenanceTriggerSource.Manual;
        var setterConfig = productDeployment.MaintenanceSetterConfig;
        SetterResult? setterResult = null;

        if (targetMode == OperationMode.Maintenance && fireSetter)
        {
            setterResult = await _maintenanceSetterService.ApplyAsync(
                setterConfig, MaintenanceState.Maintenance, cancellationToken);

            // Optional grace window: let the product drain clients before containers stop.
            if (setterConfig is { GracePeriod.Ticks: > 0 })
            {
                _logger.LogInformation(
                    "Waiting {GraceSeconds}s grace period before stopping containers for {ProductDeploymentId}",
                    setterConfig.GracePeriod.TotalSeconds, request.ProductDeploymentId);
                await Task.Delay(setterConfig.GracePeriod, cancellationToken);
            }
        }

        // Handle container lifecycle and propagate operation mode to ALL child stacks
        await HandleContainerLifecycleAsync(
            productDeployment, previousMode, targetMode, trigger, source, request.SessionId, cancellationToken);

        if (targetMode == OperationMode.Normal && fireSetter)
        {
            // Fire after containers have been started again (health verification is the caller's concern).
            setterResult = await _maintenanceSetterService.ApplyAsync(
                setterConfig, MaintenanceState.Normal, cancellationToken);
        }

        var setterError = setterResult is { Success: false } ? setterResult.Error : null;

        return ChangeProductOperationModeResponse.Ok(
            request.ProductDeploymentId, previousMode.Name, targetMode.Name, source.ToString(), setterError);
    }

    private async Task HandleContainerLifecycleAsync(
        ProductDeployment productDeployment,
        OperationMode previousMode,
        OperationMode targetMode,
        MaintenanceTrigger? trigger,
        MaintenanceTriggerSource source,
        string? sessionId,
        CancellationToken cancellationToken)
    {
        var environmentId = productDeployment.EnvironmentId.Value.ToString();
        var isEnter = targetMode == OperationMode.Maintenance;
        var isExit = previousMode == OperationMode.Maintenance && targetMode == OperationMode.Normal;
        var action = isEnter ? "enter" : "exit";

        // Every stack that may still have containers up is affected — this is the real denominator
        // for the "stack X of N" progress shown to the user. The set is deliberately wider than
        // "Running": a stack recorded as Failed because a single container never became healthy
        // still has all its other containers up, and restricting to Running skipped it wholesale,
        // leaving those containers (and their database connections) alive through maintenance.
        // A stack without a deployment stack name was never deployed, so there is nothing to touch.
        var affectedStacks = productDeployment.Stacks
            .Where(s => MaintenanceRelevantStatuses.Contains(s.Status))
            .Where(s => !string.IsNullOrEmpty(s.DeploymentStackName))
            .ToList();
        var totalStacks = affectedStacks.Count;

        // Containers that survived the whole transition, across all stacks.
        var stillRunning = new List<ContainerDto>();

        // Stacks whose state could not be read back, so nothing can be claimed about them.
        var unverifiedStacks = new List<string>();

        // Local helper: forwards a progress update to connected clients (no-op without a session).
        async Task NotifyAsync(
            string phase, string? stackName, string? stackDisplay, int stackIndex,
            string? container, int containerIndex, int totalContainers, string message)
        {
            if (sessionId == null) return;

            try
            {
                await _deploymentNotificationService.NotifyMaintenanceProgressAsync(
                    new MaintenanceProgressNotification(
                        sessionId, action, phase, stackName, stackDisplay,
                        stackIndex, totalStacks, container, containerIndex, totalContainers, message),
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "Failed to send maintenance progress for session {SessionId}", sessionId);
            }
        }

        var stackIndex = 0;

        foreach (var stack in affectedStacks)
        {
            stackIndex++;
            var currentStackIndex = stackIndex;

            try
            {
                // Propagate operation mode to child Deployment aggregate
                PropagateOperationModeToChildDeployment(stack, targetMode, trigger, source);

                // Mark the stack active before enumerating its containers (which can take a moment).
                await NotifyAsync(
                    "InProgress", stack.StackName, stack.StackDisplayName, currentStackIndex,
                    null, 0, 0,
                    isEnter
                        ? $"Stopping stack {stack.StackDisplayName}"
                        : $"Starting stack {stack.StackDisplayName}");

                // NotifyAsync is a no-op without a session, so this callback is safe to pass always.
                Task OnContainer(StackContainerProgress cp) => NotifyAsync(
                    "InProgress", stack.StackName, stack.StackDisplayName, currentStackIndex,
                    cp.ContainerName, cp.Index, cp.Total,
                    isEnter
                        ? $"Stopping {cp.ContainerName} ({cp.Index}/{cp.Total})"
                        : $"Starting {cp.ContainerName} ({cp.Index}/{cp.Total})");

                if (isEnter)
                {
                    _logger.LogInformation(
                        "Stopping containers for stack {StackName} (product maintenance)",
                        stack.StackName);

                    await _dockerService.StopStackContainersAsync(
                        environmentId, stack.DeploymentStackName!, OnContainer, cancellationToken);

                    // A stop that did not take effect must not pass as success — maintenance mode
                    // exists to release the product's resources, above all its database sessions.
                    // A verification that cannot run is not a success either: claiming the stack
                    // stopped without having looked is what made the original bug invisible.
                    try
                    {
                        stillRunning.AddRange(await EnsureStackStoppedAsync(
                            environmentId, stack.DeploymentStackName!, stack.StackName, cancellationToken));
                    }
                    catch (Exception ex) when (ex is not OperationCanceledException)
                    {
                        _logger.LogError(ex,
                            "Could not verify that the containers of stack {StackName} stopped",
                            stack.StackName);
                        unverifiedStacks.Add(stack.StackDisplayName ?? stack.StackName);
                    }
                }
                else if (isExit)
                {
                    _logger.LogInformation(
                        "Starting containers for stack {StackName} (product maintenance exit)",
                        stack.StackName);

                    await _dockerService.StartStackContainersAsync(
                        environmentId, stack.DeploymentStackName!, OnContainer, cancellationToken);
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

        // Terminal update so the UI can flip every stack to done and leave the processing view.
        // Reporting Completed while containers are still up would hide the failure entirely — the
        // operator would start a database update against a product that is still connected.
        if (stillRunning.Count > 0)
        {
            var names = string.Join(", ", stillRunning.Select(c => c.Name).Order());

            _logger.LogError(
                "Entered maintenance for product {ProductName} but {Count} container(s) are still running: {Containers}",
                productDeployment.ProductName, stillRunning.Count, names);

            await NotifyAsync(
                "Failed", null, null, totalStacks, null, 0, 0,
                $"{stillRunning.Count} container(s) could not be stopped: {names}");
            return;
        }

        if (unverifiedStacks.Count > 0)
        {
            await NotifyAsync(
                "Failed", null, null, totalStacks, null, 0, 0,
                $"Could not verify that all containers stopped: {string.Join(", ", unverifiedStacks)}");
            return;
        }

        await NotifyAsync(
            "Completed", null, null, totalStacks, null, 0, 0,
            isEnter ? "All containers stopped" : "All containers started");
    }

    /// <summary>
    /// Verifies that a stack really has no live containers left and forces the ones that remain.
    ///
    /// A single bulk stop is not enough: the container list is a snapshot taken before stopping, so
    /// a container coming up during the transition is missed; individual stop calls can fail and
    /// were only logged; and a container under a restart policy can come back up. Each round
    /// therefore re-reads the actual state instead of trusting the previous call, escalating from
    /// stop to kill. No delay between the rounds is needed — both Docker calls block until the
    /// daemon has acted on the container.
    /// </summary>
    /// <returns>Containers that are still live after all attempts. Empty is the expected result.</returns>
    private async Task<IReadOnlyList<ContainerDto>> EnsureStackStoppedAsync(
        string environmentId,
        string deploymentStackName,
        string stackName,
        CancellationToken cancellationToken)
    {
        var remaining = await GetLiveContainersAsync(environmentId, deploymentStackName, cancellationToken);
        if (remaining.Count == 0)
        {
            return remaining;
        }

        _logger.LogWarning(
            "{Count} container(s) of stack {StackName} did not stop, retrying individually: {Containers}",
            remaining.Count, stackName, string.Join(", ", remaining.Select(c => c.Name)));

        foreach (var container in remaining)
        {
            try
            {
                await _dockerService.StopContainerAsync(environmentId, container.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Retrying stop failed for container {Name} ({Id})", container.Name, container.Id);
            }
        }

        remaining = await GetLiveContainersAsync(environmentId, deploymentStackName, cancellationToken);
        if (remaining.Count == 0)
        {
            return remaining;
        }

        // A container that ignored two stops gets killed. Losing its graceful shutdown is the lesser
        // evil compared to a maintenance window that never actually starts.
        foreach (var container in remaining)
        {
            _logger.LogWarning(
                "Container {Name} ({Id}) of stack {StackName} survived two stop attempts, killing it",
                container.Name, container.Id, stackName);

            try
            {
                await _dockerService.KillContainerAsync(environmentId, container.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Killing container {Name} ({Id}) failed", container.Name, container.Id);
            }
        }

        return await GetLiveContainersAsync(environmentId, deploymentStackName, cancellationToken);
    }

    /// <summary>
    /// Containers of the given stack that a maintenance stop still has to deal with. Containers the
    /// product opted out of (<c>rsgo.maintenance=ignore</c>) and already stopped ones are excluded.
    /// </summary>
    private async Task<IReadOnlyList<ContainerDto>> GetLiveContainersAsync(
        string environmentId,
        string deploymentStackName,
        CancellationToken cancellationToken)
    {
        var containers = await _dockerService.ListContainersAsync(environmentId, cancellationToken);

        return containers
            .Where(c => MaintenanceContainerFilter.BelongsToStack(c, deploymentStackName))
            .Where(MaintenanceContainerFilter.ShouldStop)
            .ToList();
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
