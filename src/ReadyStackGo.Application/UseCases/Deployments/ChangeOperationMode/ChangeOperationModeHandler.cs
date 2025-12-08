namespace ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// Handler for changing the operation mode of a deployment.
/// </summary>
public class ChangeOperationModeHandler : IRequestHandler<ChangeOperationModeCommand, ChangeOperationModeResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IHealthNotificationService _healthNotificationService;
    private readonly ILogger<ChangeOperationModeHandler> _logger;

    public ChangeOperationModeHandler(
        IDeploymentRepository deploymentRepository,
        IHealthNotificationService healthNotificationService,
        ILogger<ChangeOperationModeHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
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
            ExecuteModeTransition(deployment, targetMode, request.Reason, request.TargetVersion);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to change operation mode for deployment {DeploymentId}", request.DeploymentId);
            return ChangeOperationModeResponse.Fail(ex.Message);
        }

        // Save changes
        _deploymentRepository.Update(deployment);
        _deploymentRepository.SaveChanges();

        _logger.LogInformation(
            "Changed operation mode for deployment {DeploymentId} from {PreviousMode} to {NewMode}",
            request.DeploymentId, previousMode.Name, targetMode.Name);

        // Send SignalR notification immediately so UI updates
        await SendHealthNotificationAsync(deployment, cancellationToken);

        return ChangeOperationModeResponse.Ok(
            request.DeploymentId, previousMode.Name, targetMode.Name);
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
                OverallStatus = deployment.Status == DeploymentStatus.Running ? "Healthy" : "Unknown",
                OperationMode = deployment.OperationMode.Name,
                HealthyServices = deployment.Services.Count(s => s.Status == "running"),
                TotalServices = deployment.Services.Count,
                StatusMessage = GetStatusMessage(deployment),
                RequiresAttention = deployment.OperationMode == OperationMode.Failed ||
                                    deployment.OperationMode == OperationMode.Maintenance,
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
                UnhealthyCount = deployment.OperationMode == OperationMode.Failed ? 1 : 0,
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
            "Migrating" => $"Migrating to version {deployment.StackVersion}",
            "Failed" => deployment.ErrorMessage ?? "Operation failed",
            "Stopped" => "Stack is stopped",
            _ => deployment.Status == DeploymentStatus.Running
                ? "All services running"
                : $"Status: {deployment.Status}"
        };
    }

    private static void ExecuteModeTransition(
        Deployment deployment,
        OperationMode targetMode,
        string? reason,
        string? targetVersion)
    {
        // Route to the appropriate domain method based on target mode
        if (targetMode == OperationMode.Maintenance)
        {
            deployment.EnterMaintenance(reason);
        }
        else if (targetMode == OperationMode.Normal)
        {
            // Determine which exit method to use based on current mode
            if (deployment.OperationMode == OperationMode.Maintenance)
            {
                deployment.ExitMaintenance();
            }
            else if (deployment.OperationMode == OperationMode.Migrating)
            {
                // If exiting migration to normal, treat as completion
                deployment.CompleteMigration(targetVersion ?? deployment.StackVersion ?? "unknown");
            }
            else if (deployment.OperationMode == OperationMode.Failed)
            {
                deployment.RecoverFromFailure();
            }
            else
            {
                throw new ArgumentException(
                    $"Cannot transition from {deployment.OperationMode.Name} to Normal");
            }
        }
        else if (targetMode == OperationMode.Migrating)
        {
            if (string.IsNullOrEmpty(targetVersion))
            {
                throw new ArgumentException("Target version is required when entering migration mode");
            }
            deployment.StartMigration(targetVersion);
        }
        else if (targetMode == OperationMode.Failed)
        {
            // Failed is typically set by system, but allow manual override
            deployment.FailMigration(reason ?? "Manual failure state");
        }
        else if (targetMode == OperationMode.Stopped)
        {
            // Stopped is set via MarkAsStopped which also changes DeploymentStatus
            throw new ArgumentException(
                "Use the stop deployment endpoint to stop a deployment");
        }
        else
        {
            throw new ArgumentException($"Unknown target mode: {targetMode.Name}");
        }
    }
}
