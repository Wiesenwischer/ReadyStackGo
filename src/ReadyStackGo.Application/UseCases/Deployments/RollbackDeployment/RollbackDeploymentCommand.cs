using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

/// <summary>
/// Command to rollback a deployment to the previous version.
/// Rollback is only available after a failed upgrade (before Point of No Return).
/// No SnapshotId needed - always rolls back to the single PendingUpgradeSnapshot.
/// </summary>
public record RollbackDeploymentCommand(
    string EnvironmentId,
    string DeploymentId) : IRequest<RollbackDeploymentResponse>;
