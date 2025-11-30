namespace ReadyStackGo.Domain.StackManagement.ValueObjects;

/// <summary>
/// Status of a deployment.
/// </summary>
public enum DeploymentStatus
{
    /// <summary>
    /// Deployment is pending.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Deployment is running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Deployment is stopped.
    /// </summary>
    Stopped = 2,

    /// <summary>
    /// Deployment failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Deployment has been removed.
    /// </summary>
    Removed = 4
}
