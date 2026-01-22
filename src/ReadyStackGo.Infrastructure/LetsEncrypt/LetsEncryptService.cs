using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Certes;
using Certes.Acme;
using Certes.Acme.Resource;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.LetsEncrypt;

/// <summary>
/// Let's Encrypt service using Certes ACME client
/// </summary>
public class LetsEncryptService : ILetsEncryptService
{
    private readonly IConfigStore _configStore;
    private readonly IDnsProviderFactory _dnsProviderFactory;
    private readonly IPendingChallengeStore _challengeStore;
    private readonly ILogger<LetsEncryptService> _logger;
    private readonly string _configPath;

    private const string AccountKeyFileName = "acme_account.pem";
    private const string LetsEncryptCertFileName = "letsencrypt.pfx";

    // Track pending order for manual DNS challenge completion
    private static AcmeContext? _pendingAcmeContext;
    private static IOrderContext? _pendingOrder;
    private static IKey? _pendingPrivateKey;

    public LetsEncryptService(
        IConfigStore configStore,
        IDnsProviderFactory dnsProviderFactory,
        IPendingChallengeStore challengeStore,
        IConfiguration configuration,
        ILogger<LetsEncryptService> logger)
    {
        _configStore = configStore;
        _dnsProviderFactory = dnsProviderFactory;
        _challengeStore = challengeStore;
        _logger = logger;
        _configPath = configuration.GetValue<string>("ConfigPath") ?? "/app/config";
    }

    public async Task<LetsEncryptResult> RequestCertificateAsync(
        LetsEncryptRequest request,
        CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Requesting Let's Encrypt certificate for domains: {Domains} (Staging: {UseStaging}, Challenge: {ChallengeType})",
                string.Join(", ", request.Domains), request.UseStaging, request.ChallengeType);

            // Clear any existing pending challenges
            await _challengeStore.ClearAllAsync();

            // Save configuration early
            var tlsConfig = await _configStore.GetTlsConfigAsync();
            tlsConfig.LetsEncrypt = new LetsEncryptConfig
            {
                Domains = request.Domains.ToList(),
                Email = request.Email,
                UseStaging = request.UseStaging,
                ChallengeType = MapChallengeType(request.ChallengeType),
                DnsProvider = MapDnsProviderConfig(request.DnsProvider),
                LastRenewalAttempt = DateTime.UtcNow
            };
            await _configStore.SaveTlsConfigAsync(tlsConfig);

            // Get or create ACME account
            var acme = await GetOrCreateAcmeContextAsync(request.Email, request.UseStaging);

            // Create order for domains
            var order = await acme.NewOrder(request.Domains.ToArray());

            // Process authorizations based on challenge type
            if (request.ChallengeType == LetsEncryptChallengeType.Http01)
            {
                return await ProcessHttp01ChallengesAsync(acme, order, tlsConfig, cancellationToken);
            }
            else
            {
                return await ProcessDns01ChallengesAsync(acme, order, request, tlsConfig, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to request Let's Encrypt certificate");

            // Update config with error
            await UpdateConfigWithErrorAsync(ex.Message);

            return LetsEncryptResult.Error($"Failed to request certificate: {ex.Message}");
        }
    }

    private async Task<LetsEncryptResult> ProcessHttp01ChallengesAsync(
        AcmeContext acme,
        IOrderContext order,
        TlsConfig tlsConfig,
        CancellationToken cancellationToken)
    {
        var authorizations = await order.Authorizations();

        foreach (var authz in authorizations)
        {
            var httpChallenge = await authz.Http();

            // Store challenge response for HTTP endpoint
            await _challengeStore.SetHttpChallengeAsync(httpChallenge.Token, httpChallenge.KeyAuthz);

            _logger.LogDebug("HTTP-01 challenge stored: Token={Token}", httpChallenge.Token);

            // Validate challenge
            var challenge = await httpChallenge.Validate();

            // Wait for validation
            var result = await WaitForChallengeValidationAsync(httpChallenge, cancellationToken);

            // Clean up challenge
            await _challengeStore.RemoveHttpChallengeAsync(httpChallenge.Token);

            if (result.Status != ChallengeStatus.Valid)
            {
                var error = result.Error?.Detail ?? "Challenge validation failed";
                _logger.LogError("HTTP-01 challenge failed: {Error}", error);
                return LetsEncryptResult.Error($"HTTP-01 challenge failed: {error}");
            }
        }

        // Generate and save certificate
        return await GenerateAndSaveCertificateAsync(acme, order, tlsConfig);
    }

    private async Task<LetsEncryptResult> ProcessDns01ChallengesAsync(
        AcmeContext acme,
        IOrderContext order,
        LetsEncryptRequest request,
        TlsConfig tlsConfig,
        CancellationToken cancellationToken)
    {
        var dnsProvider = _dnsProviderFactory.Create(MapDnsProviderConfig(request.DnsProvider));
        var isManualDns = dnsProvider.ProviderType == DnsProviderType.Manual;
        var recordIds = new List<(string domain, string recordId)>();

        try
        {
            var authorizations = await order.Authorizations();

            foreach (var authz in authorizations)
            {
                var dnsChallenge = await authz.Dns();
                var authzResource = await authz.Resource();
                var domain = $"_acme-challenge.{authzResource.Identifier.Value}";
                var dnsValue = acme.AccountKey.DnsTxt(dnsChallenge.Token);

                var recordId = await dnsProvider.CreateTxtRecordAsync(domain, dnsValue, cancellationToken);
                recordIds.Add((domain, recordId));

                _logger.LogDebug("DNS-01 challenge created: Domain={Domain}, Value={Value}", domain, dnsValue);
            }

            // For manual DNS, return and wait for user confirmation
            if (isManualDns)
            {
                // Store pending state for later completion
                _pendingAcmeContext = acme;
                _pendingOrder = order;
                _pendingPrivateKey = KeyFactory.NewKey(KeyAlgorithm.RS256);

                return LetsEncryptResult.AwaitingDns(
                    "DNS challenges created. Please add the TXT records shown below, then confirm to continue.");
            }

            // For automated DNS, wait for propagation and validate
            _logger.LogInformation("Waiting for DNS propagation (60 seconds)...");
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationToken);

            // Validate all challenges
            foreach (var authz in authorizations)
            {
                var dnsChallenge = await authz.Dns();
                await dnsChallenge.Validate();

                var result = await WaitForChallengeValidationAsync(dnsChallenge, cancellationToken);

                if (result.Status != ChallengeStatus.Valid)
                {
                    var error = result.Error?.Detail ?? "DNS challenge validation failed";
                    _logger.LogError("DNS-01 challenge failed: {Error}", error);
                    return LetsEncryptResult.Error($"DNS-01 challenge failed: {error}");
                }
            }

            // Generate and save certificate
            return await GenerateAndSaveCertificateAsync(acme, order, tlsConfig);
        }
        finally
        {
            // Clean up DNS records (for automated providers)
            if (!isManualDns)
            {
                foreach (var (domain, recordId) in recordIds)
                {
                    try
                    {
                        await dnsProvider.DeleteTxtRecordAsync(domain, recordId, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to clean up DNS record {RecordId}", recordId);
                    }
                }
            }
        }
    }

    public async Task<LetsEncryptResult> ConfirmManualDnsChallengesAsync(CancellationToken cancellationToken = default)
    {
        if (_pendingAcmeContext == null || _pendingOrder == null)
        {
            return LetsEncryptResult.Error("No pending DNS challenges to confirm");
        }

        try
        {
            _logger.LogInformation("Confirming manual DNS challenges...");

            var tlsConfig = await _configStore.GetTlsConfigAsync();
            var authorizations = await _pendingOrder.Authorizations();

            // Validate all challenges
            foreach (var authz in authorizations)
            {
                var dnsChallenge = await authz.Dns();
                await dnsChallenge.Validate();

                var result = await WaitForChallengeValidationAsync(dnsChallenge, cancellationToken);

                if (result.Status != ChallengeStatus.Valid)
                {
                    var error = result.Error?.Detail ?? "DNS challenge validation failed";
                    _logger.LogError("DNS-01 challenge failed: {Error}", error);
                    return LetsEncryptResult.Error($"DNS-01 challenge failed: {error}. Please verify the TXT records are correct and DNS has propagated.");
                }
            }

            // Generate and save certificate
            var generateResult = await GenerateAndSaveCertificateAsync(_pendingAcmeContext, _pendingOrder, tlsConfig);

            // Clear pending state and challenges
            _pendingAcmeContext = null;
            _pendingOrder = null;
            _pendingPrivateKey = null;
            await _challengeStore.ClearAllAsync();

            return generateResult;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to confirm manual DNS challenges");
            return LetsEncryptResult.Error($"Failed to confirm DNS challenges: {ex.Message}");
        }
    }

    private async Task<LetsEncryptResult> GenerateAndSaveCertificateAsync(
        AcmeContext acme,
        IOrderContext order,
        TlsConfig tlsConfig)
    {
        var privateKey = _pendingPrivateKey ?? KeyFactory.NewKey(KeyAlgorithm.RS256);
        var domains = tlsConfig.LetsEncrypt?.Domains ?? new List<string>();

        var cert = await order.Generate(new CsrInfo
        {
            CommonName = domains.FirstOrDefault() ?? "localhost"
        }, privateKey);

        // Save certificate as PFX
        var certPath = await SaveCertificateAsync(cert, privateKey);

        // Update configuration
        tlsConfig.TlsMode = TlsMode.LetsEncrypt;
        tlsConfig.CertificatePath = certPath;
        if (tlsConfig.LetsEncrypt != null)
        {
            tlsConfig.LetsEncrypt.LastIssuedAt = DateTime.UtcNow;
            tlsConfig.LetsEncrypt.LastError = null;
        }

        await _configStore.SaveTlsConfigAsync(tlsConfig);

        // Get expiration date
        var password = await GetCertPasswordAsync();
        using var x509Cert = X509CertificateLoader.LoadPkcs12(await File.ReadAllBytesAsync(certPath), password);

        _logger.LogInformation(
            "Let's Encrypt certificate issued successfully. Expires: {ExpiresAt}",
            x509Cert.NotAfter);

        return LetsEncryptResult.Ok(certPath, x509Cert.NotAfter,
            "Certificate issued successfully. Application restart required to apply the new certificate.");
    }

    public async Task<bool> NeedsRenewalAsync(int daysBeforeExpiry = 30)
    {
        var tlsConfig = await _configStore.GetTlsConfigAsync();

        if (tlsConfig.TlsMode != TlsMode.LetsEncrypt)
            return false;

        if (!File.Exists(tlsConfig.CertificatePath))
            return true;

        try
        {
            var password = await GetCertPasswordAsync();
            using var cert = X509CertificateLoader.LoadPkcs12(await File.ReadAllBytesAsync(tlsConfig.CertificatePath), password);

            return DateTime.UtcNow.AddDays(daysBeforeExpiry) > cert.NotAfter;
        }
        catch
        {
            return true;
        }
    }

    public async Task<LetsEncryptResult> RenewCertificateAsync(CancellationToken cancellationToken = default)
    {
        var tlsConfig = await _configStore.GetTlsConfigAsync();
        var leConfig = tlsConfig.LetsEncrypt;

        if (leConfig == null || leConfig.Domains.Count == 0 || string.IsNullOrEmpty(leConfig.Email))
        {
            return LetsEncryptResult.Error("Let's Encrypt is not configured");
        }

        _logger.LogInformation("Renewing Let's Encrypt certificate");

        return await RequestCertificateAsync(new LetsEncryptRequest
        {
            Domains = leConfig.Domains,
            Email = leConfig.Email,
            UseStaging = leConfig.UseStaging,
            ChallengeType = MapChallengeTypeBack(leConfig.ChallengeType),
            DnsProvider = MapDnsProviderConfigBack(leConfig.DnsProvider)
        }, cancellationToken);
    }

    public async Task<LetsEncryptStatus> GetStatusAsync()
    {
        var tlsConfig = await _configStore.GetTlsConfigAsync();
        var leConfig = tlsConfig.LetsEncrypt;

        DateTime? expiresAt = null;
        if (tlsConfig.TlsMode == TlsMode.LetsEncrypt && File.Exists(tlsConfig.CertificatePath))
        {
            try
            {
                var password = await GetCertPasswordAsync();
                using var cert = X509CertificateLoader.LoadPkcs12(await File.ReadAllBytesAsync(tlsConfig.CertificatePath), password);
                expiresAt = cert.NotAfter;
            }
            catch { }
        }

        var pendingChallenges = await _challengeStore.GetPendingDnsChallengesAsync();

        return new LetsEncryptStatus
        {
            IsConfigured = leConfig != null && leConfig.Domains.Count > 0,
            IsActive = tlsConfig.TlsMode == TlsMode.LetsEncrypt,
            Domains = leConfig?.Domains.AsReadOnly() ?? (IReadOnlyList<string>)Array.Empty<string>(),
            CertificateExpiresAt = expiresAt,
            LastIssuedAt = leConfig?.LastIssuedAt,
            LastRenewalAttempt = leConfig?.LastRenewalAttempt,
            LastError = leConfig?.LastError,
            IsUsingStaging = leConfig?.UseStaging ?? false,
            ChallengeType = leConfig?.ChallengeType.ToString() ?? "Http01",
            PendingDnsChallenges = pendingChallenges.Select(c => new PendingDnsChallengeInfo
            {
                Domain = c.Domain,
                TxtRecordName = c.TxtRecordName,
                TxtValue = c.TxtValue,
                CreatedAt = c.CreatedAt
            }).ToList()
        };
    }

    public async Task<DomainValidationResult> ValidateDomainsAsync(
        IReadOnlyList<string> domains,
        CancellationToken cancellationToken = default)
    {
        var errors = new Dictionary<string, string?>();

        foreach (var domain in domains)
        {
            try
            {
                var hostEntry = await Dns.GetHostEntryAsync(domain, cancellationToken);
                errors[domain] = null; // Valid
            }
            catch (Exception ex)
            {
                errors[domain] = $"DNS resolution failed: {ex.Message}";
            }
        }

        return new DomainValidationResult
        {
            IsValid = errors.Values.All(v => v == null),
            DomainErrors = errors
        };
    }

    private async Task<AcmeContext> GetOrCreateAcmeContextAsync(string email, bool useStaging)
    {
        var acmeServer = useStaging ? WellKnownServers.LetsEncryptStagingV2 : WellKnownServers.LetsEncryptV2;
        var tlsPath = Path.Combine(_configPath, "tls");
        var accountKeyPath = Path.Combine(tlsPath, AccountKeyFileName);

        System.IO.Directory.CreateDirectory(tlsPath);

        AcmeContext acme;

        if (File.Exists(accountKeyPath))
        {
            var keyPem = await File.ReadAllTextAsync(accountKeyPath);
            var accountKey = KeyFactory.FromPem(keyPem);
            acme = new AcmeContext(acmeServer, accountKey);

            try
            {
                // Verify account exists
                await acme.Account();
                _logger.LogDebug("Using existing ACME account");
            }
            catch
            {
                // Account doesn't exist, create new one
                _logger.LogInformation("Creating new ACME account for {Email}", email);
                acme = new AcmeContext(acmeServer);
                await acme.NewAccount(email, termsOfServiceAgreed: true);
                await File.WriteAllTextAsync(accountKeyPath, acme.AccountKey.ToPem());
            }
        }
        else
        {
            // Create new account
            _logger.LogInformation("Creating new ACME account for {Email}", email);
            acme = new AcmeContext(acmeServer);
            await acme.NewAccount(email, termsOfServiceAgreed: true);
            await File.WriteAllTextAsync(accountKeyPath, acme.AccountKey.ToPem());
        }

        return acme;
    }

    private async Task<Challenge> WaitForChallengeValidationAsync(
        IChallengeContext challenge,
        CancellationToken cancellationToken,
        int maxAttempts = 30)
    {
        for (int i = 0; i < maxAttempts; i++)
        {
            var resource = await challenge.Resource();

            _logger.LogDebug("Challenge status: {Status}", resource.Status);

            if (resource.Status == ChallengeStatus.Valid || resource.Status == ChallengeStatus.Invalid)
                return resource;

            await Task.Delay(TimeSpan.FromSeconds(2), cancellationToken);
        }

        throw new TimeoutException("Challenge validation timed out after 60 seconds");
    }

    private async Task<string> SaveCertificateAsync(CertificateChain cert, IKey privateKey)
    {
        var tlsPath = Path.Combine(_configPath, "tls");
        System.IO.Directory.CreateDirectory(tlsPath);

        var certPath = Path.Combine(tlsPath, LetsEncryptCertFileName);
        var password = await GetOrCreateCertPasswordAsync();

        var pfxBuilder = cert.ToPfx(privateKey);
        var pfxBytes = pfxBuilder.Build("letsencrypt", password);

        await File.WriteAllBytesAsync(certPath, pfxBytes);

        return certPath;
    }

    private async Task<string> GetCertPasswordAsync()
    {
        var passwordPath = Path.Combine(_configPath, "tls", ".cert_password");
        return File.Exists(passwordPath) ? await File.ReadAllTextAsync(passwordPath) : string.Empty;
    }

    private async Task<string> GetOrCreateCertPasswordAsync()
    {
        var passwordPath = Path.Combine(_configPath, "tls", ".cert_password");

        if (File.Exists(passwordPath))
            return await File.ReadAllTextAsync(passwordPath);

        var password = GenerateSecurePassword();
        System.IO.Directory.CreateDirectory(Path.GetDirectoryName(passwordPath)!);
        await File.WriteAllTextAsync(passwordPath, password);
        return password;
    }

    private async Task UpdateConfigWithErrorAsync(string error)
    {
        try
        {
            var tlsConfig = await _configStore.GetTlsConfigAsync();
            if (tlsConfig.LetsEncrypt != null)
            {
                tlsConfig.LetsEncrypt.LastRenewalAttempt = DateTime.UtcNow;
                tlsConfig.LetsEncrypt.LastError = error;
                await _configStore.SaveTlsConfigAsync(tlsConfig);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to update config with error");
        }
    }

    private static string GenerateSecurePassword(int length = 32)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789!@#$%^&*";
        var random = new byte[length];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(random);
        return new string(random.Select(b => chars[b % chars.Length]).ToArray());
    }

    private static AcmeChallengeType MapChallengeType(LetsEncryptChallengeType type) =>
        type switch
        {
            LetsEncryptChallengeType.Http01 => AcmeChallengeType.Http01,
            LetsEncryptChallengeType.Dns01 => AcmeChallengeType.Dns01,
            _ => AcmeChallengeType.Http01
        };

    private static LetsEncryptChallengeType MapChallengeTypeBack(AcmeChallengeType type) =>
        type switch
        {
            AcmeChallengeType.Http01 => LetsEncryptChallengeType.Http01,
            AcmeChallengeType.Dns01 => LetsEncryptChallengeType.Dns01,
            _ => LetsEncryptChallengeType.Http01
        };

    private static DnsProviderConfig? MapDnsProviderConfig(LetsEncryptDnsProviderConfig? config)
    {
        if (config == null) return null;
        return new DnsProviderConfig
        {
            Type = config.Type switch
            {
                LetsEncryptDnsProviderType.Cloudflare => DnsProviderType.Cloudflare,
                _ => DnsProviderType.Manual
            },
            CloudflareApiToken = config.CloudflareApiToken,
            CloudflareZoneId = config.CloudflareZoneId
        };
    }

    private static LetsEncryptDnsProviderConfig? MapDnsProviderConfigBack(DnsProviderConfig? config)
    {
        if (config == null) return null;
        return new LetsEncryptDnsProviderConfig
        {
            Type = config.Type switch
            {
                DnsProviderType.Cloudflare => LetsEncryptDnsProviderType.Cloudflare,
                _ => LetsEncryptDnsProviderType.Manual
            },
            CloudflareApiToken = config.CloudflareApiToken,
            CloudflareZoneId = config.CloudflareZoneId
        };
    }
}
