namespace ReadyStackGo.Domain.Deployment.ProductDeployments;

/// <summary>
/// Status of a product deployment lifecycle.
///
/// State machine:
///   Deploying  → Running | PartiallyRunning | Failed
///   Running    → Upgrading | Removing
///   PartiallyRunning → Upgrading | Removing
///   Upgrading  → Running | PartiallyRunning | Failed
///   Failed     → Upgrading | Removing
///   Removing   → Removed (terminal)
/// </summary>
public enum ProductDeploymentStatus
{
    /// <summary>
    /// Initial deployment of all stacks is in progress.
    /// </summary>
    Deploying = 0,

    /// <summary>
    /// All stacks are deployed and running.
    /// </summary>
    Running = 1,

    /// <summary>
    /// Some stacks are running, some failed or pending.
    /// </summary>
    PartiallyRunning = 2,

    /// <summary>
    /// Upgrade of all stacks is in progress.
    /// </summary>
    Upgrading = 3,

    /// <summary>
    /// Critical error or all stacks failed.
    /// </summary>
    Failed = 4,

    /// <summary>
    /// Removal of all stacks is in progress.
    /// </summary>
    Removing = 5,

    /// <summary>
    /// All stacks have been removed (terminal state).
    /// </summary>
    Removed = 6
}
