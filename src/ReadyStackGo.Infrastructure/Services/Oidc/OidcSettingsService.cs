using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Oidc;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services.Oidc;

/// <summary>
/// Persists OIDC provider configuration in rsgo.oidc.json via <see cref="IConfigStore"/>,
/// encrypting client secrets at rest with <see cref="ICredentialEncryptionService"/>.
/// Writes run under a process-wide lock (read-modify-write of one file).
/// </summary>
public class OidcSettingsService : IOidcSettingsService
{
    private static readonly SemaphoreSlim WriteLock = new(1, 1);

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
        var provider = Find(config, name);
        return provider == null ? null : Map(provider);
    }

    public Task AddAsync(OidcProviderSettings provider, CancellationToken cancellationToken = default) =>
        WriteAsync(config =>
        {
            if (Find(config, provider.Name) != null)
            {
                throw new InvalidOperationException($"An OIDC provider named '{provider.Name}' already exists.");
            }
            config.Providers.Add(ToConfig(provider, existingEncryptedSecret: null));
        }, cancellationToken);

    public Task UpdateAsync(OidcProviderSettings provider, CancellationToken cancellationToken = default) =>
        WriteAsync(config =>
        {
            var existing = Find(config, provider.Name)
                ?? throw new KeyNotFoundException($"OIDC provider '{provider.Name}' does not exist.");
            var index = config.Providers.IndexOf(existing);
            var updated = ToConfig(provider, existing.EncryptedClientSecret);
            // The stored name keeps its original spelling (older entries may use upper case).
            updated.Name = existing.Name;
            config.Providers[index] = updated;
        }, cancellationToken);

    public async Task<bool> RemoveAsync(string name, CancellationToken cancellationToken = default)
    {
        var removed = false;
        await WriteAsync(config =>
        {
            var existing = Find(config, name);
            if (existing != null)
            {
                removed = config.Providers.Remove(existing);
            }
        }, cancellationToken);
        return removed;
    }

    public Task RecordResultAsync(string name, OidcLastResult result, bool? reconnectNeeded = null, CancellationToken cancellationToken = default) =>
        WriteAsync(config =>
        {
            var existing = Find(config, name);
            if (existing == null)
            {
                return;
            }
            existing.LastResult = new OidcLastResultConfig
            {
                At = result.At,
                Kind = result.Kind,
                Passed = result.Passed,
                Message = result.Message
            };
            if (reconnectNeeded.HasValue)
            {
                existing.ReconnectNeeded = reconnectNeeded.Value;
            }
        }, cancellationToken);

    private async Task WriteAsync(Action<OidcConfig> change, CancellationToken cancellationToken)
    {
        await WriteLock.WaitAsync(cancellationToken);
        try
        {
            var config = await _configStore.GetOidcConfigAsync();
            change(config);
            await _configStore.SaveOidcConfigAsync(config);
        }
        finally
        {
            WriteLock.Release();
        }
    }

    private static OidcProviderConfig? Find(OidcConfig config, string name) =>
        config.Providers.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));

    private OidcProviderConfig ToConfig(OidcProviderSettings p, string? existingEncryptedSecret) => new()
    {
        Name = p.Name,
        DisplayName = p.DisplayName,
        Authority = p.Authority,
        ClientId = p.ClientId,
        EncryptedClientSecret = string.IsNullOrEmpty(p.ClientSecret)
            ? existingEncryptedSecret
            : _encryption.Encrypt(p.ClientSecret),
        Scopes = string.IsNullOrWhiteSpace(p.Scopes) ? OidcProviderSettings.DefaultScopes : p.Scopes,
        Enabled = p.Enabled,
        Template = p.Template,
        Registration = p.Registration,
        RequirePar = p.RequirePar,
        Claims = new OidcClaimsConfig
        {
            Username = p.Claims.Username,
            DisplayName = p.Claims.DisplayName,
            Email = p.Claims.Email
        },
        PairedAt = p.PairedAt,
        PairedBy = p.PairedBy,
        TrustUnverifiedEmail = p.TrustUnverifiedEmail,
        TestedSignIn = p.TestedSignIn == null
            ? null
            : new OidcTestedSignInConfig { At = p.TestedSignIn.At, Fingerprint = p.TestedSignIn.Fingerprint },
        LastResult = p.LastResult == null
            ? null
            : new OidcLastResultConfig
            {
                At = p.LastResult.At,
                Kind = p.LastResult.Kind,
                Passed = p.LastResult.Passed,
                Message = p.LastResult.Message
            },
        ReconnectNeeded = p.ReconnectNeeded
    };

    private OidcProviderSettings Map(OidcProviderConfig c)
    {
        var defaults = new OidcClaimNames();
        return new OidcProviderSettings
        {
            Name = c.Name,
            DisplayName = c.DisplayName,
            Authority = c.Authority,
            ClientId = c.ClientId,
            ClientSecret = string.IsNullOrEmpty(c.EncryptedClientSecret)
                ? null
                : _encryption.Decrypt(c.EncryptedClientSecret),
            Scopes = c.Scopes,
            Enabled = c.Enabled,
            Template = string.IsNullOrWhiteSpace(c.Template) ? OidcProviderSettings.GenericTemplateId : c.Template,
            Registration = string.IsNullOrWhiteSpace(c.Registration) ? RegistrationKinds.Manual : c.Registration,
            RequirePar = c.RequirePar ?? false,
            Claims = new OidcClaimNames
            {
                Username = string.IsNullOrWhiteSpace(c.Claims?.Username) ? defaults.Username : c.Claims.Username,
                DisplayName = string.IsNullOrWhiteSpace(c.Claims?.DisplayName) ? defaults.DisplayName : c.Claims.DisplayName,
                Email = string.IsNullOrWhiteSpace(c.Claims?.Email) ? defaults.Email : c.Claims.Email
            },
            PairedAt = c.PairedAt,
            PairedBy = c.PairedBy,
            TrustUnverifiedEmail = c.TrustUnverifiedEmail ?? true,
            TestedSignIn = c.TestedSignIn == null
                ? null
                : new OidcTestedSignIn(c.TestedSignIn.At, c.TestedSignIn.Fingerprint),
            LastResult = c.LastResult == null
                ? null
                : new OidcLastResult(c.LastResult.At, c.LastResult.Kind, c.LastResult.Passed, c.LastResult.Message),
            ReconnectNeeded = c.ReconnectNeeded ?? false
        };
    }
}
