using MediatR;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

/// <summary>
/// Handler for rolling back a deployment to its previous version.
/// Rollback is only available after a failed upgrade (before Point of No Return).
/// </summary>
public class RollbackDeploymentHandler : IRequestHandler<RollbackDeploymentCommand, RollbackDeploymentResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;

    public RollbackDeploymentHandler(IDeploymentRepository deploymentRepository)
    {
        _deploymentRepository = deploymentRepository;
    }

    public Task<RollbackDeploymentResponse> Handle(RollbackDeploymentCommand request, CancellationToken cancellationToken)
    {
        // Validate deployment ID
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Invalid deployment ID format."
            });
        }

        // Validate environment ID
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Invalid environment ID format."
            });
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);
        var deployment = _deploymentRepository.GetById(deploymentId);

        if (deployment == null)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Deployment not found."
            });
        }

        // Verify environment access
        var environmentId = new EnvironmentId(envGuid);
        if (deployment.EnvironmentId != environmentId)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Deployment not found in this environment."
            });
        }

        // Check deployment status - rollback only available after failed upgrade
        if (deployment.Status != DeploymentStatus.Failed)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Rollback only available after failed upgrade (before container start)."
            });
        }

        // Check if rollback is possible (has pending snapshot)
        if (!deployment.CanRollback())
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "No snapshot available for rollback. The upgrade may have passed the Point of No Return (container start)."
            });
        }

        var previousVersion = deployment.StackVersion;
        var targetVersion = deployment.GetRollbackTargetVersion();

        try
        {
            // Execute rollback (restores from PendingUpgradeSnapshot and clears it)
            deployment.RollbackToPrevious();
            _deploymentRepository.Update(deployment);
            _deploymentRepository.SaveChanges();

            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = true,
                Message = $"Rolled back from {previousVersion} to {targetVersion}. The deployment is now ready for re-deployment.",
                DeploymentId = request.DeploymentId,
                TargetVersion = targetVersion,
                PreviousVersion = previousVersion
            });
        }
        catch (InvalidOperationException ex)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = ex.Message
            });
        }
    }
}
