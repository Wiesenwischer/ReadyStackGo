namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Status of an individual stack within a product deployment.
/// </summary>
public enum StackDeploymentStatus
{
    /// <summary>
    /// Stack has not been started yet.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Stack deployment is in progress.
    /// </summary>
    Deploying = 1,

    /// <summary>
    /// Stack is deployed and running.
    /// </summary>
    Running = 2,

    /// <summary>
    /// Stack deployment failed.
    /// </summary>
    Failed = 3,

    /// <summary>
    /// Stack has been removed.
    /// </summary>
    Removed = 4
}
