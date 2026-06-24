using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Edge;
using ReadyStackGo.Domain.Deployment.Edge;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services.Edge;

/// <summary>
/// Resolves the TLS material for a product edge. RSGO owns the certificates and never lets
/// the edge run ACME itself; the cert is injected inline into Caddy via the admin API.
///
/// <list type="bullet">
/// <item><see cref="EdgeTlsMode.SelfSigned"/>: RSGO generates a per-hostname self-signed cert,
/// persists it under the config dir, and auto-renews it when it nears expiry.</item>
/// <item><see cref="EdgeTlsMode.Reuse"/> / <see cref="EdgeTlsMode.LetsEncrypt"/>: reuse RSGO's
/// own managed certificate (LetsEncrypt relies on RSGO's existing ACME renewal). Falls back to
/// a self-signed cert if RSGO's cert cannot be loaded.</item>
/// <item><see cref="EdgeTlsMode.Custom"/>: load an operator-provided PFX referenced by
/// <see cref="EdgeConfig.TlsCertRef"/> from the edge cert store; falls back to self-signed.</item>
/// </list>
/// </summary>
public class EdgeCertificateProvider : IEdgeCertificateProvider
{
    private const int RenewBeforeDays = 30;
    private const int SelfSignedValidityDays = 397; // CA/B max for leaf certs

    private readonly IConfigStore _configStore;
    private readonly ILogger<EdgeCertificateProvider> _logger;
    private readonly string _edgeTlsDir;

    public EdgeCertificateProvider(
        IConfigStore configStore,
        IConfiguration configuration,
        ILogger<EdgeCertificateProvider> logger)
    {
        _configStore = configStore;
        _logger = logger;
        var configPath = configuration.GetValue<string>("ConfigPath") ?? "/app/config";
        _edgeTlsDir = Path.Combine(configPath, "tls", "edge");
    }

    public async Task<EdgeCertMaterial?> GetCertificateAsync(EdgeConfig config, CancellationToken cancellationToken = default)
    {
        if (config.TlsMode == EdgeTlsMode.None)
            return null;

        try
        {
            return config.TlsMode switch
            {
                EdgeTlsMode.SelfSigned => GetOrCreateSelfSigned(config.PublicHostname),
                EdgeTlsMode.Custom => LoadCustomOrFallback(config),
                EdgeTlsMode.Reuse or EdgeTlsMode.LetsEncrypt => await ReuseRsgoOrFallbackAsync(config),
                _ => null
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to materialize edge certificate for {Hostname} (mode {Mode})",
                config.PublicHostname, config.TlsMode);
            return null;
        }
    }

    private EdgeCertMaterial GetOrCreateSelfSigned(string hostname)
    {
        Directory.CreateDirectory(_edgeTlsDir);
        var pfxPath = Path.Combine(_edgeTlsDir, $"{Sanitize(hostname)}.pfx");
        var pwPath = pfxPath + ".pw";

        // Reuse the persisted cert unless it is missing or nearing expiry.
        if (File.Exists(pfxPath) && File.Exists(pwPath))
        {
            try
            {
                using var existing = X509CertificateLoader.LoadPkcs12(
                    File.ReadAllBytes(pfxPath), File.ReadAllText(pwPath), X509KeyStorageFlags.Exportable);
                if (existing.NotAfter.ToUniversalTime() > DateTime.UtcNow.AddDays(RenewBeforeDays))
                    return ToMaterial(existing);

                _logger.LogInformation("Edge self-signed cert for {Hostname} near expiry — regenerating", hostname);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not load persisted edge cert for {Hostname} — regenerating", hostname);
            }
        }

        var (cert, password) = GenerateSelfSigned(hostname);
        using (cert)
        {
            File.WriteAllBytes(pfxPath, cert.Export(X509ContentType.Pfx, password));
            File.WriteAllText(pwPath, password);
            return ToMaterial(cert);
        }
    }

    private EdgeCertMaterial LoadCustomOrFallback(EdgeConfig config)
    {
        if (!string.IsNullOrWhiteSpace(config.TlsCertRef))
        {
            var pfxPath = Path.Combine(_edgeTlsDir, $"{Sanitize(config.TlsCertRef!)}.pfx");
            var pwPath = pfxPath + ".pw";
            if (File.Exists(pfxPath))
            {
                var pw = File.Exists(pwPath) ? File.ReadAllText(pwPath) : string.Empty;
                using var cert = X509CertificateLoader.LoadPkcs12(File.ReadAllBytes(pfxPath), pw, X509KeyStorageFlags.Exportable);
                return ToMaterial(cert);
            }

            _logger.LogWarning(
                "Edge custom cert '{CertRef}' not found in {Dir} — falling back to self-signed for {Hostname}",
                config.TlsCertRef, _edgeTlsDir, config.PublicHostname);
        }

        return GetOrCreateSelfSigned(config.PublicHostname);
    }

    private async Task<EdgeCertMaterial> ReuseRsgoOrFallbackAsync(EdgeConfig config)
    {
        try
        {
            var tlsConfig = await _configStore.GetTlsConfigAsync();
            if (File.Exists(tlsConfig.CertificatePath))
            {
                var dir = Path.GetDirectoryName(tlsConfig.CertificatePath)!;
                var pwPath = Path.Combine(dir, ".cert_password");
                var pw = File.Exists(pwPath) ? await File.ReadAllTextAsync(pwPath) : string.Empty;
                using var cert = X509CertificateLoader.LoadPkcs12(
                    await File.ReadAllBytesAsync(tlsConfig.CertificatePath), pw, X509KeyStorageFlags.Exportable);
                return ToMaterial(cert);
            }

            _logger.LogWarning("RSGO certificate not found for edge reuse — falling back to self-signed for {Hostname}",
                config.PublicHostname);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reuse RSGO certificate for edge {Hostname} — falling back to self-signed",
                config.PublicHostname);
        }

        return GetOrCreateSelfSigned(config.PublicHostname);
    }

    private static (X509Certificate2 Cert, string Password) GenerateSelfSigned(string hostname)
    {
        using var rsa = RSA.Create(2048);
        var request = new CertificateRequest($"CN={hostname}", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName(hostname);
        request.CertificateExtensions.Add(san.Build());
        request.CertificateExtensions.Add(
            new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, critical: true));
        request.CertificateExtensions.Add(
            new X509EnhancedKeyUsageExtension(new OidCollection { new Oid("1.3.6.1.5.5.7.3.1") }, critical: false));

        var cert = request.CreateSelfSigned(
            DateTimeOffset.UtcNow.AddDays(-1), DateTimeOffset.UtcNow.AddDays(SelfSignedValidityDays));

        var password = GenerateSecurePassword();
        // Round-trip through PFX so the returned instance is fully exportable.
        var pfx = cert.Export(X509ContentType.Pfx, password);
        cert.Dispose();
        return (X509CertificateLoader.LoadPkcs12(pfx, password, X509KeyStorageFlags.Exportable), password);
    }

    private static EdgeCertMaterial ToMaterial(X509Certificate2 cert)
    {
        using var key = cert.GetRSAPrivateKey()
            ?? throw new InvalidOperationException("Edge certificate has no RSA private key.");

        var certPem = cert.ExportCertificatePem();
        var keyPem = key.ExportPkcs8PrivateKeyPem();

        return new EdgeCertMaterial(certPem, keyPem, cert.NotAfter.ToUniversalTime(), cert.Thumbprint);
    }

    private static string Sanitize(string raw)
    {
        var chars = raw.ToLowerInvariant().Select(c => char.IsLetterOrDigit(c) ? c : '-').ToArray();
        return new string(chars).Trim('-');
    }

    private static string GenerateSecurePassword(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        var bytes = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        var result = new char[length];
        for (var i = 0; i < length; i++)
            result[i] = chars[bytes[i] % chars.Length];
        return new string(result);
    }
}
