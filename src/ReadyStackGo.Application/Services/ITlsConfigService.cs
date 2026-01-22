namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for TLS certificate management and configuration.
/// </summary>
public interface ITlsConfigService
{
    /// <summary>
    /// Gets current TLS configuration and certificate information.
    /// </summary>
    Task<TlsConfigInfo> GetTlsConfigAsync();

    /// <summary>
    /// Gets certificate information from the currently configured certificate.
    /// </summary>
    Task<CertificateInfo?> GetCertificateInfoAsync();

    /// <summary>
    /// Uploads and validates a custom PFX certificate.
    /// </summary>
    Task<TlsUpdateResult> UploadPfxCertificateAsync(byte[] pfxData, string password);

    /// <summary>
    /// Uploads and validates a custom PEM certificate with private key.
    /// </summary>
    Task<TlsUpdateResult> UploadPemCertificateAsync(string certificatePem, string privateKeyPem);

    /// <summary>
    /// Enables or disables HTTP access.
    /// </summary>
    Task<TlsUpdateResult> SetHttpEnabledAsync(bool enabled);

    /// <summary>
    /// Resets to self-signed certificate.
    /// </summary>
    Task<TlsUpdateResult> ResetToSelfSignedAsync();

    /// <summary>
    /// Updates reverse proxy configuration.
    /// </summary>
    Task<TlsUpdateResult> UpdateReverseProxyAsync(ReverseProxyUpdate update);
}

/// <summary>
/// Reverse proxy update request
/// </summary>
public record ReverseProxyUpdate
{
    public bool? Enabled { get; init; }
    public string? SslMode { get; init; }
    public bool? TrustForwardedFor { get; init; }
    public bool? TrustForwardedProto { get; init; }
    public bool? TrustForwardedHost { get; init; }
    public List<string>? KnownProxies { get; init; }
    public int? ForwardLimit { get; init; }
    public string? PathBase { get; init; }
}

public record TlsConfigInfo
{
    public required string Mode { get; init; }
    public CertificateInfo? CertificateInfo { get; init; }
    public bool HttpEnabled { get; init; }
    public bool RequiresRestart { get; init; }
    public ReverseProxyInfo? ReverseProxy { get; init; }
}

public record ReverseProxyInfo
{
    public bool Enabled { get; init; }
    public string SslMode { get; init; } = "Termination";
    public bool TrustForwardedFor { get; init; }
    public bool TrustForwardedProto { get; init; }
    public bool TrustForwardedHost { get; init; }
    public List<string> KnownProxies { get; init; } = new();
    public int? ForwardLimit { get; init; }
    public string? PathBase { get; init; }
}

public record CertificateInfo
{
    public required string Subject { get; init; }
    public required string Issuer { get; init; }
    public required DateTime ExpiresAt { get; init; }
    public required string Thumbprint { get; init; }
    public bool IsSelfSigned { get; init; }
    public bool IsExpired => DateTime.UtcNow > ExpiresAt;
    public bool IsExpiringSoon => DateTime.UtcNow.AddDays(30) > ExpiresAt;
}

public record TlsUpdateResult
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public bool RequiresRestart { get; init; }

    public static TlsUpdateResult Ok(string message, bool requiresRestart = true)
        => new() { Success = true, Message = message, RequiresRestart = requiresRestart };

    public static TlsUpdateResult Error(string message)
        => new() { Success = false, Message = message };
}
