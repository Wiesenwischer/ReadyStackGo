using MediatR;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

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

        // Validate snapshot ID
        if (!Guid.TryParse(request.SnapshotId, out var snapshotGuid))
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Invalid snapshot ID format."
            });
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);
        var deployment = _deploymentRepository.GetWithSnapshots(deploymentId);

        if (deployment == null)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Deployment not found."
            });
        }

        // Verify environment access
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Invalid environment ID format."
            });
        }

        var environmentId = new EnvironmentId(envGuid);
        if (deployment.EnvironmentId != environmentId)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Deployment not found in this environment."
            });
        }

        // Check if rollback is possible
        if (!deployment.CanRollback())
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Rollback is not possible for this deployment. Either no snapshots exist or the deployment is in a terminal state."
            });
        }

        // Find the snapshot
        var snapshotId = DeploymentSnapshotId.FromGuid(snapshotGuid);
        var snapshot = deployment.GetSnapshot(snapshotId);

        if (snapshot == null)
        {
            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = false,
                Message = "Snapshot not found."
            });
        }

        var previousVersion = deployment.StackVersion;
        var targetVersion = snapshot.StackVersion;

        try
        {
            // Initiate rollback (this sets the deployment to Pending state)
            deployment.RollbackTo(snapshotId);
            _deploymentRepository.Update(deployment);
            _deploymentRepository.SaveChanges();

            return Task.FromResult(new RollbackDeploymentResponse
            {
                Success = true,
                Message = $"Rollback to version {targetVersion} initiated. The deployment will be re-deployed with the snapshot configuration.",
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
