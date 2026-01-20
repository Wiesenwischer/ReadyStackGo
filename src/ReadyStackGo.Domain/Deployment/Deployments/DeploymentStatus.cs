namespace ReadyStackGo.Domain.Deployment.Deployments;

/// <summary>
/// Status of a deployment lifecycle.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// New installation is in progress.
    /// </summary>
    Installing = 0,

    /// <summary>
    /// Upgrade or rollback is in progress.
    /// </summary>
    Upgrading = 1,

    /// <summary>
    /// Deployment is running and operational.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Last operation failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Deployment has been removed (terminal state).
    /// </summary>
    Removed = 4
}
