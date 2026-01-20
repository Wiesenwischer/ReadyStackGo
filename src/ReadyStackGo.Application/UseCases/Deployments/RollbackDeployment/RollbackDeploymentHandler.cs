using MediatR;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

/// <summary>
/// Handler for rolling back a deployment after a failed upgrade.
/// Rollback simply redeploys the current version using existing deployment data
/// (StackId, StackVersion, Variables are all preserved in the Deployment entity).
/// Delegates to DeployStackCommand to leverage existing progress notifications.
/// </summary>
public class RollbackDeploymentHandler : IRequestHandler<RollbackDeploymentCommand, RollbackDeploymentResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<RollbackDeploymentHandler> _logger;

    public RollbackDeploymentHandler(
        IDeploymentRepository deploymentRepository,
        IMediator mediator,
        ILogger<RollbackDeploymentHandler> logger)
    {
        _deploymentRepository = deploymentRepository;
        _mediator = mediator;
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
            // Convert variables to dictionary
            var variablesDict = variables.ToDictionary(kv => kv.Key, kv => kv.Value);

            // Execute deployment via DeployStack command - this handles progress notifications
            var deployResult = await _mediator.Send(new DeployStackCommand(
                request.EnvironmentId,
                stackId,
                deployment.StackName,
                variablesDict,
                request.SessionId), cancellationToken);

            if (!deployResult.Success)
            {
                _logger.LogError("Rollback deployment failed for {DeploymentId}: {Message}",
                    request.DeploymentId, deployResult.Message);

                return new RollbackDeploymentResponse
                {
                    Success = false,
                    Message = $"Rollback failed: {deployResult.Message}",
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
}
