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
}

public record TlsConfigInfo
{
    public required string Mode { get; init; }
    public CertificateInfo? CertificateInfo { get; init; }
    public bool HttpEnabled { get; init; }
    public bool RequiresRestart { get; init; }
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
