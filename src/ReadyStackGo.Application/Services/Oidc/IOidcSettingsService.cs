namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>
/// Reads and persists OIDC provider configuration (client secrets encrypted at rest).
/// All writes are serialized, because sign-ins write results too.
/// </summary>
public interface IOidcSettingsService
{
    Task<IReadOnlyList<OidcProviderSettings>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<OidcProviderSettings?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>Adds a provider. Throws <see cref="InvalidOperationException"/> if the name is taken.</summary>
    Task AddAsync(OidcProviderSettings provider, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the stored provider with the same name. A null or empty client secret keeps
    /// the stored secret. Throws <see cref="KeyNotFoundException"/> if the provider is unknown.
    /// </summary>
    Task UpdateAsync(OidcProviderSettings provider, CancellationToken cancellationToken = default);

    /// <summary>Removes a provider. Returns false if it did not exist.</summary>
    Task<bool> RemoveAsync(string name, CancellationToken cancellationToken = default);

    /// <summary>
    /// Records the latest result for a provider and optionally sets or clears "reconnect needed".
    /// Unknown providers are ignored.
    /// </summary>
    Task RecordResultAsync(string name, OidcLastResult result, bool? reconnectNeeded = null, CancellationToken cancellationToken = default);
}
