namespace ReadyStackGo.Application.Services;

/// <summary>
/// Distribution-specific initialization logic that runs on application startup.
/// All operations must be idempotent (safe to run on every startup).
/// </summary>
public interface IBootstrapper
{
    /// <summary>
    /// Execute bootstrap operations (seed data, create defaults, etc.).
    /// Called during application startup after database creation.
    /// </summary>
    Task BootstrapAsync(CancellationToken cancellationToken = default);
}
