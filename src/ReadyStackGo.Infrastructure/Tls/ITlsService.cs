namespace ReadyStackGo.Infrastructure.Tls;

/// <summary>
/// Service for TLS certificate management
/// </summary>
public interface ITlsService
{
    /// <summary>
    /// Generate a self-signed certificate for development/bootstrap
    /// </summary>
    Task<string> GenerateSelfSignedCertificateAsync(string hostname = "localhost");

    /// <summary>
    /// Check if a certificate exists at the configured path
    /// </summary>
    Task<bool> CertificateExistsAsync(string certificatePath);

    /// <summary>
    /// Bootstrap TLS configuration on first start
    /// </summary>
    Task BootstrapTlsAsync();
}
