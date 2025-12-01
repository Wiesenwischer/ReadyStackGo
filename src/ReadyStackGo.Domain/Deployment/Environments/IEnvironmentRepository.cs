namespace ReadyStackGo.Domain.Deployment.Environments;

using ReadyStackGo.Domain.IdentityAccess.Organizations;
using ReadyStackGo.Domain.IdentityAccess.Users;

/// <summary>
/// Repository interface for Environment aggregate.
/// </summary>
public interface IEnvironmentRepository
{
    /// <summary>
    /// Generates a new unique environment identity.
    /// </summary>
    EnvironmentId NextIdentity();

    /// <summary>
    /// Adds a new environment.
    /// </summary>
    void Add(Environment environment);

    /// <summary>
    /// Updates an existing environment.
    /// </summary>
    void Update(Environment environment);

    /// <summary>
    /// Gets an environment by its ID.
    /// </summary>
    Environment? Get(EnvironmentId id);

    /// <summary>
    /// Gets an environment by its name within an organization.
    /// </summary>
    Environment? GetByName(OrganizationId organizationId, string name);

    /// <summary>
    /// Gets all environments for an organization.
    /// </summary>
    IEnumerable<Environment> GetByOrganization(OrganizationId organizationId);

    /// <summary>
    /// Gets the default environment for an organization.
    /// </summary>
    Environment? GetDefault(OrganizationId organizationId);

    /// <summary>
    /// Removes an environment.
    /// </summary>
    void Remove(Environment environment);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    void SaveChanges();
}
