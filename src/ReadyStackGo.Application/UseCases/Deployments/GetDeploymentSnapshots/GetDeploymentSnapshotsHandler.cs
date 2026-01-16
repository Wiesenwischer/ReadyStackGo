using MediatR;
using ReadyStackGo.Domain.Deployment.Deployments;
using ReadyStackGo.Domain.Deployment.Environments;

namespace ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

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
        var deployment = _deploymentRepository.GetWithSnapshots(deploymentId);

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

        var snapshotDtos = deployment.Snapshots
            .OrderByDescending(s => s.CreatedAt)
            .Select(s => new DeploymentSnapshotDto
            {
                SnapshotId = s.Id.Value.ToString(),
                StackVersion = s.StackVersion,
                CreatedAt = s.CreatedAt,
                Description = s.Description,
                ServiceCount = s.Services.Count,
                VariableCount = s.Variables.Count
            })
            .ToList();

        return Task.FromResult(new GetDeploymentSnapshotsResponse
        {
            Success = true,
            DeploymentId = request.DeploymentId,
            StackName = deployment.StackName,
            CurrentVersion = deployment.StackVersion,
            CanRollback = deployment.CanRollback(),
            Snapshots = snapshotDtos
        });
    }
}
