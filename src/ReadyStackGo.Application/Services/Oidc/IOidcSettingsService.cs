namespace ReadyStackGo.Application.Services.Oidc;

/// <summary>Reads and persists OIDC provider configuration (client secrets encrypted at rest).</summary>
public interface IOidcSettingsService
{
    Task<IReadOnlyList<OidcProviderSettings>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<OidcProviderSettings?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task SaveAllAsync(IEnumerable<OidcProviderSettings> providers, CancellationToken cancellationToken = default);
}
