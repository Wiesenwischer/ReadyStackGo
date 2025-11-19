using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Domain.Configuration;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Tls;

/// <summary>
/// TLS service that handles self-signed certificate generation and management
/// </summary>
public class TlsService : ITlsService
{
    private readonly IConfigStore _configStore;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TlsService> _logger;
    private readonly string _configPath;

    public TlsService(
        IConfigStore configStore,
        IConfiguration configuration,
        ILogger<TlsService> logger)
    {
        _configStore = configStore;
        _configuration = configuration;
        _logger = logger;
        _configPath = configuration.GetValue<string>("ConfigPath") ?? "/app/config";
    }

    public async Task<string> GenerateSelfSignedCertificateAsync(string hostname = "localhost")
    {
        try
        {
            _logger.LogInformation("Generating self-signed certificate for {Hostname}", hostname);

            // Create directory for TLS certificates
            var tlsPath = Path.Combine(_configPath, "tls");
            if (!Directory.Exists(tlsPath))
            {
                Directory.CreateDirectory(tlsPath);
            }

            var certPath = Path.Combine(tlsPath, "selfsigned.pfx");

            // Generate RSA key pair
            using var rsa = RSA.Create(2048);

            // Create certificate request
            var request = new CertificateRequest(
                $"CN={hostname}",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);

            // Add SAN (Subject Alternative Names)
            var sanBuilder = new SubjectAlternativeNameBuilder();
            sanBuilder.AddDnsName(hostname);
            sanBuilder.AddDnsName("localhost");
            sanBuilder.AddIpAddress(System.Net.IPAddress.Loopback);
            sanBuilder.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
            request.CertificateExtensions.Add(sanBuilder.Build());

            // Add Key Usage
            request.CertificateExtensions.Add(
                new X509KeyUsageExtension(
                    X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment,
                    critical: true));

            // Add Enhanced Key Usage for Server Authentication
            request.CertificateExtensions.Add(
                new X509EnhancedKeyUsageExtension(
                    new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, // Server Authentication
                    critical: true));

            // Create self-signed certificate valid for 365 days
            using var certificate = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddDays(365));

            // Export to PFX with password
            var password = GenerateSecurePassword();
            var pfxBytes = certificate.Export(X509ContentType.Pfx, password);

            // Save certificate to file
            await File.WriteAllBytesAsync(certPath, pfxBytes);

            _logger.LogInformation("Self-signed certificate generated successfully at {Path}", certPath);

            // Update TLS configuration
            var tlsConfig = await _configStore.GetTlsConfigAsync();
            tlsConfig.TlsMode = TlsMode.SelfSigned;
            tlsConfig.CertificatePath = certPath;
            await _configStore.SaveTlsConfigAsync(tlsConfig);

            // Store password securely (in production, use a secrets manager)
            var passwordPath = Path.Combine(tlsPath, ".cert_password");
            await File.WriteAllTextAsync(passwordPath, password);

            return certPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate self-signed certificate");
            throw;
        }
    }

    public async Task<bool> CertificateExistsAsync(string certificatePath)
    {
        return await Task.FromResult(File.Exists(certificatePath));
    }

    public async Task BootstrapTlsAsync()
    {
        try
        {
            _logger.LogInformation("Bootstrapping TLS configuration");

            var tlsConfig = await _configStore.GetTlsConfigAsync();

            // Check if certificate already exists
            if (await CertificateExistsAsync(tlsConfig.CertificatePath))
            {
                _logger.LogInformation("TLS certificate already exists at {Path}", tlsConfig.CertificatePath);
                return;
            }

            // Generate new self-signed certificate
            await GenerateSelfSignedCertificateAsync();

            _logger.LogInformation("TLS bootstrap completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TLS bootstrap failed");
            throw;
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
