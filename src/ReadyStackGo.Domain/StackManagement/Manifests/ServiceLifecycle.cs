namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Defines the lifecycle type of a service container.
/// </summary>
public enum ServiceLifecycle
{
    /// <summary>
    /// Long-running service (default).
    /// The container is expected to run continuously and be monitored via health checks.
    /// </summary>
    Service,

    /// <summary>
    /// Run-once initialization container (e.g., database migrator).
    /// The container runs to completion before regular services start.
    /// Does not restart on failure (restart: no) - deployment aborts if init fails.
    /// To retry a failed init container, re-deploy the stack.
    /// Health checks are not applicable.
    /// </summary>
    Init
}
