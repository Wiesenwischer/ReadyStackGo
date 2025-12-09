namespace ReadyStackGo.Domain.Deployment.Observers;

/// <summary>
/// Interface for maintenance observers.
/// Implementations check external systems to determine if maintenance mode should be active.
///
/// Each implementation is registered via DI with a keyed service using the ObserverType value.
/// This allows for easy extensibility - new observer types only need a new implementation
/// and DI registration, no changes to existing code.
/// </summary>
public interface IMaintenanceObserver
{
    /// <summary>
    /// The type of observer this implementation handles.
    /// </summary>
    ObserverType Type { get; }

    /// <summary>
    /// Performs the maintenance check and returns the result.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The result of the maintenance check.</returns>
    Task<ObserverResult> CheckAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Factory for creating maintenance observer instances.
/// Uses DI to resolve the appropriate implementation based on ObserverType.
/// </summary>
public interface IMaintenanceObserverFactory
{
    /// <summary>
    /// Creates a maintenance observer for the given configuration.
    /// </summary>
    /// <param name="config">Observer configuration.</param>
    /// <returns>A configured observer instance.</returns>
    IMaintenanceObserver Create(MaintenanceObserverConfig config);

    /// <summary>
    /// Checks if an observer implementation exists for the given type.
    /// </summary>
    /// <param name="type">The observer type to check.</param>
    /// <returns>True if an implementation is registered.</returns>
    bool IsSupported(ObserverType type);

    /// <summary>
    /// Gets all supported observer types.
    /// </summary>
    IEnumerable<ObserverType> SupportedTypes { get; }
}
