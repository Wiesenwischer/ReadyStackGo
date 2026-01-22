namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// TLS configuration stored in rsgo.tls.json
/// </summary>
public class TlsConfig
{
    public TlsMode TlsMode { get; set; } = TlsMode.SelfSigned;
    public string CertificatePath { get; set; } = "/app/config/tls/selfsigned.pfx";
    public int Port { get; set; } = 5001;
    public bool HttpEnabled { get; set; } = true;
    public string? TerminatingContext { get; set; }

    /// <summary>
    /// Let's Encrypt configuration (only used when TlsMode is LetsEncrypt)
    /// </summary>
    public LetsEncryptConfig? LetsEncrypt { get; set; }
}

public enum TlsMode
{
    SelfSigned,
    Custom,
    LetsEncrypt
}

/// <summary>
/// Let's Encrypt ACME configuration
/// </summary>
public class LetsEncryptConfig
{
    /// <summary>
    /// Domain names for the certificate (primary + SANs)
    /// </summary>
    public List<string> Domains { get; set; } = new();

    /// <summary>
    /// Email for Let's Encrypt account registration
    /// </summary>
    public string? Email { get; set; }

    /// <summary>
    /// Use staging environment for testing (certificates won't be trusted)
    /// </summary>
    public bool UseStaging { get; set; } = false;

    /// <summary>
    /// Challenge type: Http01 or Dns01
    /// </summary>
    public AcmeChallengeType ChallengeType { get; set; } = AcmeChallengeType.Http01;

    /// <summary>
    /// DNS provider configuration (for DNS-01 challenge)
    /// </summary>
    public DnsProviderConfig? DnsProvider { get; set; }

    /// <summary>
    /// Days before expiration to trigger renewal (default: 30)
    /// </summary>
    public int RenewalDaysBeforeExpiry { get; set; } = 30;

    /// <summary>
    /// Last successful certificate issuance
    /// </summary>
    public DateTime? LastIssuedAt { get; set; }

    /// <summary>
    /// Last renewal attempt (success or failure)
    /// </summary>
    public DateTime? LastRenewalAttempt { get; set; }

    /// <summary>
    /// Last error message if renewal failed
    /// </summary>
    public string? LastError { get; set; }
}

public enum AcmeChallengeType
{
    Http01,
    Dns01
}

/// <summary>
/// DNS provider configuration for DNS-01 challenges
/// </summary>
public class DnsProviderConfig
{
    public DnsProviderType Type { get; set; } = DnsProviderType.Manual;

    /// <summary>
    /// Cloudflare API token (for Cloudflare provider)
    /// </summary>
    public string? CloudflareApiToken { get; set; }

    /// <summary>
    /// Cloudflare Zone ID (optional, auto-detected if not set)
    /// </summary>
    public string? CloudflareZoneId { get; set; }
}

public enum DnsProviderType
{
    Manual,
    Cloudflare
}
