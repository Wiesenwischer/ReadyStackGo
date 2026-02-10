namespace ReadyStackGo.Domain.IdentityAccess.ApiKeys;

using ReadyStackGo.Domain.IdentityAccess.Organizations;

/// <summary>
/// Repository interface for ApiKey aggregate.
/// </summary>
public interface IApiKeyRepository
{
    /// <summary>
    /// Gets an API key by its ID.
    /// </summary>
    ApiKey? GetById(ApiKeyId id);

    /// <summary>
    /// Gets an API key by its SHA-256 hash. Used for authentication lookup.
    /// </summary>
    ApiKey? GetByKeyHash(string keyHash);

    /// <summary>
    /// Gets all API keys for an organization.
    /// </summary>
    IEnumerable<ApiKey> GetByOrganization(OrganizationId organizationId);

    /// <summary>
    /// Adds a new API key.
    /// </summary>
    void Add(ApiKey apiKey);

    /// <summary>
    /// Updates an existing API key.
    /// </summary>
    void Update(ApiKey apiKey);

    /// <summary>
    /// Removes an API key.
    /// </summary>
    void Remove(ApiKey apiKey);

    /// <summary>
    /// Saves all pending changes.
    /// </summary>
    void SaveChanges();
}
