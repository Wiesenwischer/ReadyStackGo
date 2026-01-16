using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

/// <summary>
/// Query to get all snapshots for a deployment.
/// </summary>
public record GetDeploymentSnapshotsQuery(string EnvironmentId, string DeploymentId) : IRequest<GetDeploymentSnapshotsResponse>;
