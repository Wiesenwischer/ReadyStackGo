namespace ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// Handler for changing the operation mode of a deployment.
/// Supports transitions between Normal and Maintenance modes only.
/// Entering maintenance mode stops containers, exiting starts them again.
/// Containers with label rsgo.maintenance=ignore are not affected.
/// </summary>
public class ChangeOperationModeHandler : IRequestHandler<ChangeOperationModeCommand, ChangeOperationModeResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IDockerService _dockerService;
    private readonly IHealthNotificationService _healthNotificationService;
    private readonly ILogger<ChangeOperationModeHandler> _logger;

    public ChangeOperationModeHandler(
        IDeploymentRepository deploymentRepository,
        IDockerService dockerService,
        IHealthNotificationService healthNotificationService,
        ILogger<ChangeOperationModeHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _dockerService = dockerService;
        _healthNotificationService = healthNotificationService;
        _logger = logger;
    }

    public async Task<ChangeOperationModeResponse> Handle(
        ChangeOperationModeCommand request,
        CancellationToken cancellationToken)
    {
        // Parse deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return ChangeOperationModeResponse.Fail("Invalid deployment ID format");
        }

        var deploymentId = new DeploymentId(deploymentGuid);
        var deployment = _deploymentRepository.Get(deploymentId);

        if (deployment == null)
        {
            return ChangeOperationModeResponse.Fail("Deployment not found");
        }

        // Validate deployment is running - OperationMode only applies to running deployments
        if (deployment.Status != DeploymentStatus.Running)
        {
            return ChangeOperationModeResponse.Fail(
                $"Cannot change operation mode. Deployment is {deployment.Status}, must be Running.");
        }

        // Parse target mode
        if (!OperationMode.TryFromName(request.NewMode, out var targetMode) || targetMode == null)
        {
            var validModes = string.Join(", ", OperationMode.GetAll().Select(m => m.Name));
            return ChangeOperationModeResponse.Fail(
                $"Invalid operation mode '{request.NewMode}'. Valid modes: {validModes}");
        }

        var previousMode = deployment.OperationMode;

        // Check if already in target mode
        if (previousMode == targetMode)
        {
            return ChangeOperationModeResponse.Ok(
                request.DeploymentId, previousMode.Name, targetMode.Name);
        }

        // Execute the appropriate transition
        try
        {
            ExecuteModeTransition(deployment, targetMode, request.Reason);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to change operation mode for deployment {DeploymentId}", request.DeploymentId);
            return ChangeOperationModeResponse.Fail(ex.Message);
        }

        // Save changes - no need for Update() since entity is already tracked from Get() query
        _deploymentRepository.SaveChanges();

        _logger.LogInformation(
            "Changed operation mode for deployment {DeploymentId} from {PreviousMode} to {NewMode}",
            request.DeploymentId, previousMode.Name, targetMode.Name);

        // Send SignalR notification immediately so UI updates
        await SendHealthNotificationAsync(deployment, cancellationToken);

        // Handle container lifecycle based on mode transition
        await HandleContainerLifecycleAsync(deployment, previousMode, targetMode, cancellationToken);

        return ChangeOperationModeResponse.Ok(
            request.DeploymentId, previousMode.Name, targetMode.Name);
    }

    /// <summary>
    /// Handles container stop/start based on operation mode transitions.
    /// - Entering Maintenance: Stop all containers (except rsgo.maintenance=ignore)
    /// - Exiting Maintenance to Normal: Start all containers
    /// </summary>
    private async Task HandleContainerLifecycleAsync(
        Deployment deployment,
        OperationMode previousMode,
        OperationMode targetMode,
        CancellationToken cancellationToken)
    {
        var environmentId = deployment.EnvironmentId.Value.ToString();
        var stackName = deployment.StackName;

        try
        {
            // Entering Maintenance Mode -> Stop containers
            if (targetMode == OperationMode.Maintenance)
            {
                _logger.LogInformation(
                    "Entering maintenance mode for {StackName} - stopping containers",
                    stackName);

                var stoppedContainers = await _dockerService.StopStackContainersAsync(
                    environmentId, stackName, cancellationToken);

                _logger.LogInformation(
                    "Stopped {Count} containers for {StackName} maintenance",
                    stoppedContainers.Count, stackName);
            }
            // Exiting Maintenance Mode -> Start containers
            else if (previousMode == OperationMode.Maintenance && targetMode == OperationMode.Normal)
            {
                _logger.LogInformation(
                    "Exiting maintenance mode for {StackName} - starting containers",
                    stackName);

                var startedContainers = await _dockerService.StartStackContainersAsync(
                    environmentId, stackName, cancellationToken);

                _logger.LogInformation(
                    "Started {Count} containers for {StackName} after maintenance",
                    startedContainers.Count, stackName);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail the operation - mode change was successful
            _logger.LogWarning(ex,
                "Failed to manage containers during mode transition for {StackName}. " +
                "Mode changed successfully but container state may need manual intervention.",
                stackName);
        }
    }

    private async Task SendHealthNotificationAsync(Deployment deployment, CancellationToken cancellationToken)
    {
        try
        {
            // Create a health summary with the updated operation mode
            var healthSummary = new StackHealthSummaryDto
            {
                DeploymentId = deployment.Id.Value.ToString(),
                StackName = deployment.StackName,
                CurrentVersion = deployment.StackVersion,
                OverallStatus = deployment.OperationMode == OperationMode.Normal ? "Healthy" : "Degraded",
                OperationMode = deployment.OperationMode.Name,
                HealthyServices = deployment.Services.Count(s => s.Status == "running"),
                TotalServices = deployment.Services.Count,
                StatusMessage = GetStatusMessage(deployment),
                RequiresAttention = deployment.OperationMode == OperationMode.Maintenance,
                CapturedAtUtc = DateTime.UtcNow
            };

            // Notify clients subscribed to this deployment
            await _healthNotificationService.NotifyDeploymentHealthChangedAsync(
                deployment.Id, healthSummary, cancellationToken);

            // Also notify via environment channel (UI subscribes to this)
            var environmentSummary = new EnvironmentHealthSummaryDto
            {
                EnvironmentId = deployment.EnvironmentId.Value.ToString(),
                EnvironmentName = "", // Not available here, but not critical for UI update
                TotalStacks = 1,
                HealthyCount = deployment.OperationMode == OperationMode.Normal ? 1 : 0,
                DegradedCount = deployment.OperationMode == OperationMode.Maintenance ? 1 : 0,
                UnhealthyCount = 0,
                Stacks = new List<StackHealthSummaryDto> { healthSummary }
            };

            await _healthNotificationService.NotifyEnvironmentHealthChangedAsync(
                deployment.EnvironmentId, environmentSummary, cancellationToken);

            // Also notify via global channel
            await _healthNotificationService.NotifyGlobalHealthChangedAsync(
                healthSummary, cancellationToken);

            _logger.LogDebug(
                "Sent SignalR notification for operation mode change on deployment {DeploymentId}",
                deployment.Id);
        }
        catch (Exception ex)
        {
            // Don't fail the operation if notification fails
            _logger.LogWarning(ex,
                "Failed to send SignalR notification for deployment {DeploymentId}",
                deployment.Id);
        }
    }

    private static string GetStatusMessage(Deployment deployment)
    {
        return deployment.OperationMode.Name switch
        {
            "Maintenance" => "Stack is in maintenance mode",
            _ => "All services running"
        };
    }

    private static void ExecuteModeTransition(
        Deployment deployment,
        OperationMode targetMode,
        string? reason)
    {
        // Only two transitions are valid: Normal <-> Maintenance
        if (targetMode == OperationMode.Maintenance)
        {
            deployment.EnterMaintenance(reason);
        }
        else if (targetMode == OperationMode.Normal)
        {
            deployment.ExitMaintenance();
        }
        else
        {
            throw new ArgumentException($"Unknown target mode: {targetMode.Name}");
        }
    }
}
