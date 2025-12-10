namespace ReadyStackGo.Domain.Deployment.RuntimeConfig;

/// <summary>
/// Repository for managing runtime stack configurations.
/// </summary>
public interface IRuntimeStackConfigRepository
{
    /// <summary>
    /// Gets a runtime configuration by its ID.
    /// </summary>
    Task<RuntimeStackConfig?> GetByIdAsync(RuntimeStackConfigId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a runtime configuration for a specific stack.
    /// </summary>
    Task<RuntimeStackConfig?> GetByStackIdAsync(string stackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all runtime configurations.
    /// </summary>
    Task<IReadOnlyList<RuntimeStackConfig>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a new runtime configuration.
    /// </summary>
    Task AddAsync(RuntimeStackConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Updates an existing runtime configuration.
    /// </summary>
    Task UpdateAsync(RuntimeStackConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Saves a runtime configuration (add or update).
    /// </summary>
    Task SaveAsync(RuntimeStackConfig config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a runtime configuration.
    /// </summary>
    Task RemoveAsync(RuntimeStackConfigId id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a runtime configuration by stack ID.
    /// </summary>
    Task RemoveByStackIdAsync(string stackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a configuration exists for the given stack.
    /// </summary>
    Task<bool> ExistsForStackAsync(string stackId, CancellationToken cancellationToken = default);
}
