using Microsoft.Extensions.Options;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service for automatic Let's Encrypt certificate renewal
/// </summary>
public class CertificateRenewalBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CertificateRenewalBackgroundService> _logger;
    private readonly CertificateRenewalOptions _options;

    public CertificateRenewalBackgroundService(
        IServiceProvider serviceProvider,
        IOptions<CertificateRenewalOptions> options,
        ILogger<CertificateRenewalBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Certificate renewal background service is disabled");
            return;
        }

        _logger.LogInformation(
            "Certificate Renewal Background Service starting. Check interval: {Interval}h",
            _options.CheckIntervalHours);

        // Initial delay to let the application start up
        await Task.Delay(TimeSpan.FromMinutes(_options.InitialDelayMinutes), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAndRenewCertificateAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during certificate renewal check");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(_options.CheckIntervalHours), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Certificate Renewal Background Service stopped");
    }

    private async Task CheckAndRenewCertificateAsync(CancellationToken stoppingToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var letsEncryptService = scope.ServiceProvider.GetRequiredService<ILetsEncryptService>();
        var configStore = scope.ServiceProvider.GetRequiredService<IConfigStore>();

        var tlsConfig = await configStore.GetTlsConfigAsync();

        if (tlsConfig.TlsMode != TlsMode.LetsEncrypt)
        {
            _logger.LogDebug("TLS mode is not Let's Encrypt, skipping renewal check");
            return;
        }

        // Skip if using manual DNS challenge (requires user interaction)
        if (tlsConfig.LetsEncrypt?.ChallengeType == AcmeChallengeType.Dns01 &&
            tlsConfig.LetsEncrypt?.DnsProvider?.Type == DnsProviderType.Manual)
        {
            _logger.LogDebug("Manual DNS challenge configured, skipping automatic renewal");
            return;
        }

        var daysBeforeExpiry = tlsConfig.LetsEncrypt?.RenewalDaysBeforeExpiry ?? 30;

        if (!await letsEncryptService.NeedsRenewalAsync(daysBeforeExpiry))
        {
            _logger.LogDebug("Certificate does not need renewal");
            return;
        }

        _logger.LogInformation("Certificate needs renewal, initiating renewal process");

        var result = await letsEncryptService.RenewCertificateAsync(stoppingToken);

        if (result.Success)
        {
            _logger.LogInformation(
                "Certificate renewed successfully. New expiration: {ExpiresAt}. Restart required to apply.",
                result.ExpiresAt);
        }
        else
        {
            _logger.LogWarning("Certificate renewal failed: {Message}", result.Message);
        }
    }
}

/// <summary>
/// Options for certificate renewal background service
/// </summary>
public class CertificateRenewalOptions
{
    public const string SectionName = "CertificateRenewal";

    /// <summary>
    /// Enable automatic certificate renewal
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Hours between renewal checks (default: 12)
    /// </summary>
    public int CheckIntervalHours { get; set; } = 12;

    /// <summary>
    /// Minutes to wait before first check after startup (default: 5)
    /// </summary>
    public int InitialDelayMinutes { get; set; } = 5;
}
