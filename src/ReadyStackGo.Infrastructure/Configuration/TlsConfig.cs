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

    /// <summary>
    /// Reverse proxy configuration (for running behind nginx, Traefik, etc.)
    /// </summary>
    public ReverseProxyConfig? ReverseProxy { get; set; }
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

/// <summary>
/// SSL handling mode when behind a reverse proxy
/// </summary>
public enum ReverseProxySslMode
{
    /// <summary>
    /// SSL Termination: Proxy handles HTTPS, sends HTTP to backend.
    /// The backend does not need a certificate.
    /// </summary>
    Termination,

    /// <summary>
    /// SSL Passthrough: Proxy forwards encrypted traffic directly to backend.
    /// The backend handles TLS and needs a valid certificate.
    /// </summary>
    Passthrough,

    /// <summary>
    /// Re-Encryption: Proxy terminates SSL, then creates new HTTPS connection to backend.
    /// Both proxy and backend need certificates.
    /// </summary>
    ReEncryption
}

/// <summary>
/// Configuration for running behind a reverse proxy (nginx, Traefik, HAProxy, etc.)
/// </summary>
public class ReverseProxyConfig
{
    /// <summary>
    /// Enable reverse proxy mode. When enabled:
    /// - ForwardedHeaders middleware processes X-Forwarded-* headers
    /// - Behavior depends on SslMode setting
    /// </summary>
    public bool Enabled { get; set; } = false;

    /// <summary>
    /// How SSL is handled between proxy and this application.
    /// Default: Termination (proxy handles SSL, sends HTTP to backend)
    /// </summary>
    public ReverseProxySslMode SslMode { get; set; } = ReverseProxySslMode.Termination;

    /// <summary>
    /// Trust the X-Forwarded-For header for client IP
    /// </summary>
    public bool TrustForwardedFor { get; set; } = true;

    /// <summary>
    /// Trust the X-Forwarded-Proto header for HTTPS detection
    /// </summary>
    public bool TrustForwardedProto { get; set; } = true;

    /// <summary>
    /// Trust the X-Forwarded-Host header for host detection
    /// </summary>
    public bool TrustForwardedHost { get; set; } = true;

    /// <summary>
    /// List of known proxy IP addresses or CIDR ranges (e.g., "10.0.0.0/8", "172.16.0.0/12")
    /// If empty, all proxies are trusted (less secure but simpler for Docker networks)
    /// </summary>
    public List<string> KnownProxies { get; set; } = new();

    /// <summary>
    /// Number of proxies in front of the application (for X-Forwarded-For processing)
    /// </summary>
    public int? ForwardLimit { get; set; }

    /// <summary>
    /// Base path if the app is hosted at a subpath (e.g., "/readystackgo")
    /// </summary>
    public string? PathBase { get; set; }
}
