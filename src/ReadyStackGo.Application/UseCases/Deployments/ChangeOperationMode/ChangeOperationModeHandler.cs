namespace ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;

using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Health;

/// <summary>
/// Handler for changing the operation mode of a deployment.
/// </summary>
public class ChangeOperationModeHandler : IRequestHandler<ChangeOperationModeCommand, ChangeOperationModeResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly ILogger<ChangeOperationModeHandler> _logger;

    public ChangeOperationModeHandler(
        IDeploymentRepository deploymentRepository,
        ILogger<ChangeOperationModeHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _logger = logger;
    }

    public Task<ChangeOperationModeResponse> Handle(
        ChangeOperationModeCommand request,
        CancellationToken cancellationToken)
    {
        // Parse deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return Task.FromResult(ChangeOperationModeResponse.Fail("Invalid deployment ID format"));
        }

        var deploymentId = new DeploymentId(deploymentGuid);
        var deployment = _deploymentRepository.Get(deploymentId);

        if (deployment == null)
        {
            return Task.FromResult(ChangeOperationModeResponse.Fail("Deployment not found"));
        }

        // Parse target mode
        if (!OperationMode.TryFromName(request.NewMode, out var targetMode) || targetMode == null)
        {
            var validModes = string.Join(", ", OperationMode.GetAll().Select(m => m.Name));
            return Task.FromResult(ChangeOperationModeResponse.Fail(
                $"Invalid operation mode '{request.NewMode}'. Valid modes: {validModes}"));
        }

        var previousMode = deployment.OperationMode;

        // Check if already in target mode
        if (previousMode == targetMode)
        {
            return Task.FromResult(ChangeOperationModeResponse.Ok(
                request.DeploymentId, previousMode.Name, targetMode.Name));
        }

        // Execute the appropriate transition
        try
        {
            ExecuteModeTransition(deployment, targetMode, request.Reason, request.TargetVersion);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Failed to change operation mode for deployment {DeploymentId}", request.DeploymentId);
            return Task.FromResult(ChangeOperationModeResponse.Fail(ex.Message));
        }

        // Save changes
        _deploymentRepository.Update(deployment);
        _deploymentRepository.SaveChanges();

        _logger.LogInformation(
            "Changed operation mode for deployment {DeploymentId} from {PreviousMode} to {NewMode}",
            request.DeploymentId, previousMode.Name, targetMode.Name);

        return Task.FromResult(ChangeOperationModeResponse.Ok(
            request.DeploymentId, previousMode.Name, targetMode.Name));
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
