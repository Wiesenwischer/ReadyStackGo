namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// OIDC provider configuration stored in rsgo.oidc.json. Client secrets are stored
/// encrypted (see <see cref="OidcProviderConfig.EncryptedClientSecret"/>).
/// </summary>
public class OidcConfig
{
    public List<OidcProviderConfig> Providers { get; set; } = new();
}

public class OidcProviderConfig
{
    /// <summary>URL-safe identifier used in routes (e.g. "identityaccess").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Human-readable name shown on the login button.</summary>
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>OIDC authority / issuer base URL (used for discovery).</summary>
    public string Authority { get; set; } = string.Empty;

    public string ClientId { get; set; } = string.Empty;

    /// <summary>AES-encrypted client secret (via ICredentialEncryptionService). Null if none.</summary>
    public string? EncryptedClientSecret { get; set; }

    /// <summary>Space-separated OIDC scopes.</summary>
    public string Scopes { get; set; } = "openid email profile";

    public bool Enabled { get; set; }
}
