using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// Service for TLS configuration management including certificate upload and validation.
/// </summary>
public class TlsConfigService : ITlsConfigService
{
    private readonly IConfigStore _configStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TlsConfigService> _logger;
    private readonly string _configPath;

    public TlsConfigService(
        IConfigStore configStore,
        IConfiguration configuration,
        ILogger<TlsConfigService> logger)
    {
        _configStore = configStore;
        _configuration = configuration;
        _logger = logger;
        _configPath = configuration.GetValue<string>("ConfigPath") ?? "/app/config";
    }

    public async Task<TlsConfigInfo> GetTlsConfigAsync()
    {
        var config = await _configStore.GetTlsConfigAsync();
        var certInfo = await GetCertificateInfoAsync();

        return new TlsConfigInfo
        {
            Mode = config.TlsMode.ToString(),
            HttpEnabled = config.HttpEnabled,
            CertificateInfo = certInfo,
            RequiresRestart = false
        };
    }

    public async Task<CertificateInfo?> GetCertificateInfoAsync()
    {
        try
        {
            var config = await _configStore.GetTlsConfigAsync();

            if (!File.Exists(config.CertificatePath))
            {
                _logger.LogDebug("Certificate file not found at {Path}", config.CertificatePath);
                return null;
            }

            // Read the certificate password
            var tlsPath = Path.GetDirectoryName(config.CertificatePath);
            var passwordPath = Path.Combine(tlsPath!, ".cert_password");
            var password = File.Exists(passwordPath) ? await File.ReadAllTextAsync(passwordPath) : string.Empty;

            using var cert = new X509Certificate2(config.CertificatePath, password);

            var isSelfSigned = cert.Subject == cert.Issuer;

            return new CertificateInfo
            {
                Subject = cert.Subject,
                Issuer = cert.Issuer,
                ExpiresAt = cert.NotAfter.ToUniversalTime(),
                Thumbprint = cert.Thumbprint,
                IsSelfSigned = isSelfSigned
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to read certificate information");
            return null;
        }
    }

    public async Task<TlsUpdateResult> UploadPfxCertificateAsync(byte[] pfxData, string password)
    {
        try
        {
            // Validate the certificate
            X509Certificate2 cert;
            try
            {
                cert = new X509Certificate2(pfxData, password, X509KeyStorageFlags.Exportable);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Invalid PFX certificate or password");
                return TlsUpdateResult.Error("Invalid certificate or password. Please check the PFX file and password.");
            }

            using (cert)
            {
                // Check if certificate has a private key
                if (!cert.HasPrivateKey)
                {
                    return TlsUpdateResult.Error("The certificate does not contain a private key.");
                }

                // Check if certificate is expired
                if (DateTime.UtcNow > cert.NotAfter)
                {
                    return TlsUpdateResult.Error($"The certificate has expired on {cert.NotAfter:yyyy-MM-dd}.");
                }

                // Check if certificate is not yet valid
                if (DateTime.UtcNow < cert.NotBefore)
                {
                    return TlsUpdateResult.Error($"The certificate is not yet valid until {cert.NotBefore:yyyy-MM-dd}.");
                }

                _logger.LogInformation("Certificate validated successfully: Subject={Subject}, Expires={Expires}",
                    cert.Subject, cert.NotAfter);
            }

            // Save the certificate
            var tlsPath = Path.Combine(_configPath, "tls");
            if (!Directory.Exists(tlsPath))
            {
                Directory.CreateDirectory(tlsPath);
            }

            var certPath = Path.Combine(tlsPath, "custom.pfx");
            await File.WriteAllBytesAsync(certPath, pfxData);

            // Save the password
            var passwordPath = Path.Combine(tlsPath, ".cert_password");
            await File.WriteAllTextAsync(passwordPath, password);

            // Update configuration
            var config = await _configStore.GetTlsConfigAsync();
            config.TlsMode = TlsMode.Custom;
            config.CertificatePath = certPath;
            await _configStore.SaveTlsConfigAsync(config);

            _logger.LogInformation("Custom PFX certificate uploaded successfully");
            return TlsUpdateResult.Ok("Certificate uploaded successfully. Application restart required to apply changes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload PFX certificate");
            return TlsUpdateResult.Error($"Failed to upload certificate: {ex.Message}");
        }
    }

    public async Task<TlsUpdateResult> UploadPemCertificateAsync(string certificatePem, string privateKeyPem)
    {
        try
        {
            // Parse and validate PEM certificate
            X509Certificate2 cert;
            try
            {
                cert = X509Certificate2.CreateFromPem(certificatePem, privateKeyPem);
            }
            catch (CryptographicException ex)
            {
                _logger.LogWarning(ex, "Invalid PEM certificate or private key");
                return TlsUpdateResult.Error("Invalid certificate or private key format.");
            }

            using (cert)
            {
                // Check if certificate is expired
                if (DateTime.UtcNow > cert.NotAfter)
                {
                    return TlsUpdateResult.Error($"The certificate has expired on {cert.NotAfter:yyyy-MM-dd}.");
                }

                // Check if certificate is not yet valid
                if (DateTime.UtcNow < cert.NotBefore)
                {
                    return TlsUpdateResult.Error($"The certificate is not yet valid until {cert.NotBefore:yyyy-MM-dd}.");
                }

                _logger.LogInformation("PEM certificate validated successfully: Subject={Subject}, Expires={Expires}",
                    cert.Subject, cert.NotAfter);

                // Export to PFX for storage
                var tlsPath = Path.Combine(_configPath, "tls");
                if (!Directory.Exists(tlsPath))
                {
                    Directory.CreateDirectory(tlsPath);
                }

                var password = GenerateSecurePassword();
                var pfxData = cert.Export(X509ContentType.Pfx, password);
                var certPath = Path.Combine(tlsPath, "custom.pfx");
                await File.WriteAllBytesAsync(certPath, pfxData);

                // Save the password
                var passwordPath = Path.Combine(tlsPath, ".cert_password");
                await File.WriteAllTextAsync(passwordPath, password);

                // Update configuration
                var config = await _configStore.GetTlsConfigAsync();
                config.TlsMode = TlsMode.Custom;
                config.CertificatePath = certPath;
                await _configStore.SaveTlsConfigAsync(config);

                _logger.LogInformation("Custom PEM certificate uploaded successfully");
                return TlsUpdateResult.Ok("Certificate uploaded successfully. Application restart required to apply changes.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload PEM certificate");
            return TlsUpdateResult.Error($"Failed to upload certificate: {ex.Message}");
        }
    }

    public async Task<TlsUpdateResult> SetHttpEnabledAsync(bool enabled)
    {
        try
        {
            var config = await _configStore.GetTlsConfigAsync();
            config.HttpEnabled = enabled;
            await _configStore.SaveTlsConfigAsync(config);

            _logger.LogInformation("HTTP enabled set to {Enabled}", enabled);
            return TlsUpdateResult.Ok(
                enabled ? "HTTP access enabled." : "HTTP access disabled.",
                requiresRestart: true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update HTTP enabled setting");
            return TlsUpdateResult.Error($"Failed to update setting: {ex.Message}");
        }
    }

    public async Task<TlsUpdateResult> ResetToSelfSignedAsync()
    {
        try
        {
            var tlsPath = Path.Combine(_configPath, "tls");
            var selfSignedPath = Path.Combine(tlsPath, "selfsigned.pfx");

            // Check if self-signed certificate exists
            if (!File.Exists(selfSignedPath))
            {
                return TlsUpdateResult.Error("Self-signed certificate not found. Application restart required to regenerate.");
            }

            // Update configuration
            var config = await _configStore.GetTlsConfigAsync();
            config.TlsMode = TlsMode.SelfSigned;
            config.CertificatePath = selfSignedPath;
            await _configStore.SaveTlsConfigAsync(config);

            _logger.LogInformation("Reset to self-signed certificate");
            return TlsUpdateResult.Ok("Reset to self-signed certificate. Application restart required to apply changes.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to reset to self-signed certificate");
            return TlsUpdateResult.Error($"Failed to reset: {ex.Message}");
        }
    }

    private static string GenerateSecurePassword(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);

        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = chars[random[i] % chars.Length];
        }

        return new string(result);
    }
}
