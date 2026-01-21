namespace ReadyStackGo.Domain.Deployment.Registries;

/// <summary>
/// Repository interface for Registry aggregate.
/// </summary>
public interface IRegistryRepository
{
    /// <summary>
    /// Gets a registry by its ID.
    /// </summary>
    Registry? GetById(RegistryId id);

    /// <summary>
    /// Gets all registries for an organization.
    /// </summary>
    IEnumerable<Registry> GetByOrganization(OrganizationId organizationId);

    /// <summary>
    /// Gets the default registry for an organization.
    /// </summary>
    Registry? GetDefault(OrganizationId organizationId);

    /// <summary>
    /// Finds a registry that matches the given image reference.
    /// </summary>
    Registry? FindMatchingRegistry(OrganizationId organizationId, string imageReference);

    /// <summary>
    /// Adds a new registry.
    /// </summary>
    void Add(Registry registry);

    /// <summary>
    /// Updates an existing registry.
    /// </summary>
    void Update(Registry registry);

    /// <summary>
    /// Removes a registry.
    /// </summary>
    void Remove(Registry registry);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    void SaveChanges();
}
