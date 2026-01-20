using MediatR;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

/// <summary>
/// Handler for getting rollback information for a deployment.
/// Rollback is available when a deployment is in Failed status - it redeploys the current version
/// using the stored StackId, StackVersion, and Variables.
/// </summary>
public class GetDeploymentSnapshotsHandler : IRequestHandler<GetDeploymentSnapshotsQuery, GetDeploymentSnapshotsResponse>
{
    private readonly IDeploymentRepository _deploymentRepository;

    public GetDeploymentSnapshotsHandler(IDeploymentRepository deploymentRepository)
    {
        _deploymentRepository = deploymentRepository;
    }

    public Task<GetDeploymentSnapshotsResponse> Handle(GetDeploymentSnapshotsQuery request, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(request.DeploymentId, out var deploymentGuid))
        {
            return Task.FromResult(new GetDeploymentSnapshotsResponse
            {
                Success = false,
                Message = "Invalid deployment ID format."
            });
        }

        var deploymentId = DeploymentId.FromGuid(deploymentGuid);
        var deployment = _deploymentRepository.GetById(deploymentId);

        if (deployment == null)
        {
            return Task.FromResult(new GetDeploymentSnapshotsResponse
            {
                Success = false,
                Message = "Deployment not found."
            });
        }

        // Verify environment access
        if (!Guid.TryParse(request.EnvironmentId, out var envGuid))
        {
            return Task.FromResult(new GetDeploymentSnapshotsResponse
            {
                Success = false,
                Message = "Invalid environment ID format."
            });
        }

        var environmentId = new EnvironmentId(envGuid);
        if (deployment.EnvironmentId != environmentId)
        {
            return Task.FromResult(new GetDeploymentSnapshotsResponse
            {
                Success = false,
                Message = "Deployment not found in this environment."
            });
        }

        var response = new GetDeploymentSnapshotsResponse
        {
            Success = true,
            DeploymentId = request.DeploymentId,
            StackName = deployment.StackName,
            CurrentVersion = deployment.StackVersion,
            CanRollback = deployment.CanRollback(),
            RollbackTargetVersion = deployment.GetRollbackTargetVersion()
        };

        // Add context message based on state
        if (deployment.CanRollback())
        {
            response.Message = $"Rollback to version {deployment.GetRollbackTargetVersion()} is available.";
        }
        else if (deployment.Status != DeploymentStatus.Failed)
        {
            response.Message = "Rollback only available after failed upgrade.";
        }

        return Task.FromResult(response);
    }
}
