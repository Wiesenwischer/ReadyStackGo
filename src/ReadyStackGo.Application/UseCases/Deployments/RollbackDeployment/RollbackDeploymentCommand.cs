using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

/// <summary>
/// Command to rollback a deployment to a previous snapshot.
/// </summary>
public record RollbackDeploymentCommand(
    string EnvironmentId,
    string DeploymentId,
    string SnapshotId) : IRequest<RollbackDeploymentResponse>;
