namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// OIDC provider configuration stored in rsgo.oidc.json. Client secrets are stored
/// encrypted (see <see cref="OidcProviderConfig.EncryptedClientSecret"/>). Fields added for
/// identity provider templates are optional: entries written before read as "Generic OIDC".
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

    /// <summary>Template id; missing means "generic-oidc".</summary>
    public string? Template { get; set; }

    /// <summary>Registration kind ("manual" or "pairing"); missing means "manual".</summary>
    public string? Registration { get; set; }

    public bool? RequirePar { get; set; }

    public OidcClaimsConfig? Claims { get; set; }

    public DateTime? PairedAt { get; set; }

    public string? PairedBy { get; set; }

    /// <summary>Missing means true (behavior of providers created before this setting existed).</summary>
    public bool? TrustUnverifiedEmail { get; set; }

    public OidcTestedSignInConfig? TestedSignIn { get; set; }

    public OidcLastResultConfig? LastResult { get; set; }

    public bool? ReconnectNeeded { get; set; }
}

public class OidcClaimsConfig
{
    public string? Username { get; set; }
    public string? DisplayName { get; set; }
    public string? Email { get; set; }
}

public class OidcTestedSignInConfig
{
    public DateTime At { get; set; }
    public string Fingerprint { get; set; } = string.Empty;
}

public class OidcLastResultConfig
{
    public DateTime At { get; set; }
    public string Kind { get; set; } = string.Empty;
    public bool Passed { get; set; }
    public string? Message { get; set; }
}
