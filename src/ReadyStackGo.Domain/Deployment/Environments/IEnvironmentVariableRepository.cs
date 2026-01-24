namespace ReadyStackGo.Domain.Deployment.Environments;

/// <summary>
/// Repository interface for EnvironmentVariable entity.
/// </summary>
public interface IEnvironmentVariableRepository
{
    /// <summary>
    /// Generates a new unique environment variable identity.
    /// </summary>
    EnvironmentVariableId NextIdentity();

    /// <summary>
    /// Adds a new environment variable.
    /// </summary>
    void Add(EnvironmentVariable environmentVariable);

    /// <summary>
    /// Updates an existing environment variable.
    /// </summary>
    void Update(EnvironmentVariable environmentVariable);

    /// <summary>
    /// Gets an environment variable by its ID.
    /// </summary>
    EnvironmentVariable? Get(EnvironmentVariableId id);

    /// <summary>
    /// Gets an environment variable by environment and key.
    /// </summary>
    EnvironmentVariable? GetByEnvironmentAndKey(EnvironmentId environmentId, string key);

    /// <summary>
    /// Gets all environment variables for a specific environment.
    /// </summary>
    IEnumerable<EnvironmentVariable> GetByEnvironment(EnvironmentId environmentId);

    /// <summary>
    /// Removes an environment variable.
    /// </summary>
    void Remove(EnvironmentVariable environmentVariable);

    /// <summary>
    /// Removes all environment variables for a specific environment.
    /// </summary>
    void RemoveAllByEnvironment(EnvironmentId environmentId);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    void SaveChanges();
}
