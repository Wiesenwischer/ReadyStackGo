using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services.Oidc;

/// <summary>
/// Persists OIDC provider configuration in rsgo.oidc.json via <see cref="IConfigStore"/>,
/// encrypting client secrets at rest with <see cref="ICredentialEncryptionService"/>.
/// </summary>
public class OidcSettingsService : IOidcSettingsService
{
    private readonly IConfigStore _configStore;
    private readonly ICredentialEncryptionService _encryption;

    public OidcSettingsService(IConfigStore configStore, ICredentialEncryptionService encryption)
    {
        _configStore = configStore;
        _encryption = encryption;
    }

    public async Task<IReadOnlyList<OidcProviderSettings>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetOidcConfigAsync();
        return config.Providers.Select(Map).ToList();
    }

    public async Task<OidcProviderSettings?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetOidcConfigAsync();
        var provider = config.Providers
            .FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return provider == null ? null : Map(provider);
    }

    public async Task SaveAllAsync(IEnumerable<OidcProviderSettings> providers, CancellationToken cancellationToken = default)
    {
        var existing = await _configStore.GetOidcConfigAsync();

        var config = new OidcConfig
        {
            Providers = providers.Select(p =>
            {
                // Preserve the stored secret if the caller did not supply a new one.
                var existingProvider = existing.Providers
                    .FirstOrDefault(e => string.Equals(e.Name, p.Name, StringComparison.OrdinalIgnoreCase));

                var encryptedSecret = string.IsNullOrEmpty(p.ClientSecret)
                    ? existingProvider?.EncryptedClientSecret
                    : _encryption.Encrypt(p.ClientSecret);

                return new OidcProviderConfig
                {
                    Name = p.Name,
                    DisplayName = p.DisplayName,
                    Authority = p.Authority,
                    ClientId = p.ClientId,
                    EncryptedClientSecret = encryptedSecret,
                    Scopes = string.IsNullOrWhiteSpace(p.Scopes) ? "openid email profile" : p.Scopes,
                    Enabled = p.Enabled
                };
            }).ToList()
        };

        await _configStore.SaveOidcConfigAsync(config);
    }

    private OidcProviderSettings Map(OidcProviderConfig c) => new()
    {
        Name = c.Name,
        DisplayName = c.DisplayName,
        Authority = c.Authority,
        ClientId = c.ClientId,
        ClientSecret = string.IsNullOrEmpty(c.EncryptedClientSecret)
            ? null
            : _encryption.Decrypt(c.EncryptedClientSecret),
        Scopes = c.Scopes,
        Enabled = c.Enabled
    };
}
