namespace ReadyStackGo.Application.Environments;

/// <summary>
/// Service interface for managing environments within an organization.
/// </summary>
public interface IEnvironmentService
{
    /// <summary>
    /// Gets all environments for the organization.
    /// </summary>
    Task<ListEnvironmentsResponse> GetEnvironmentsAsync();

    /// <summary>
    /// Gets a specific environment by ID.
    /// </summary>
    Task<EnvironmentResponse?> GetEnvironmentAsync(string environmentId);

    /// <summary>
    /// Creates a new Docker Socket environment.
    /// </summary>
    Task<CreateEnvironmentResponse> CreateEnvironmentAsync(CreateEnvironmentRequest request);

    /// <summary>
    /// Updates an existing environment.
    /// </summary>
    Task<UpdateEnvironmentResponse> UpdateEnvironmentAsync(string environmentId, UpdateEnvironmentRequest request);

    /// <summary>
    /// Deletes an environment.
    /// </summary>
    Task<DeleteEnvironmentResponse> DeleteEnvironmentAsync(string environmentId);

    /// <summary>
    /// Sets an environment as the default.
    /// </summary>
    Task<SetDefaultEnvironmentResponse> SetDefaultEnvironmentAsync(string environmentId);
}
