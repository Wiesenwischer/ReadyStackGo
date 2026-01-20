namespace ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

/// <summary>
/// Response containing rollback information for a deployment.
/// Rollback redeploys the current version using the stored deployment data.
/// </summary>
public class GetDeploymentSnapshotsResponse
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? DeploymentId { get; set; }
    public string? StackName { get; set; }
    public string? CurrentVersion { get; set; }

    /// <summary>
    /// Whether rollback is currently available.
    /// True only if deployment is in Failed status with a valid version.
    /// </summary>
    public bool CanRollback { get; set; }

    /// <summary>
    /// The version that would be restored on rollback.
    /// This is the current StackVersion (unchanged since upgrade failed).
    /// Null if rollback is not available.
    /// </summary>
    public string? RollbackTargetVersion { get; set; }
}
