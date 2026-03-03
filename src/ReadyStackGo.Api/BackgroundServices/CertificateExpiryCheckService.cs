using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Api.BackgroundServices;

/// <summary>
/// Background service that checks certificate expiration and creates
/// staged in-app notifications at configured thresholds (30, 14, 7, 3, 1, 0 days).
/// Runs every 12 hours. Separate from CertificateRenewalBackgroundService
/// (different responsibility: notification vs. renewal).
/// </summary>
public class CertificateExpiryCheckService : BackgroundService
{
    private static readonly int[] Thresholds = [30, 14, 7, 3, 1, 0];

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<CertificateExpiryCheckService> _logger;

    public CertificateExpiryCheckService(
        IServiceProvider serviceProvider,
        ILogger<CertificateExpiryCheckService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Certificate Expiry Check Service starting");

        // Initial delay (1 minute)
        await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckCertificateExpiryAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during certificate expiry check");
            }

            try
            {
                await Task.Delay(TimeSpan.FromHours(12), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }

        _logger.LogInformation("Certificate Expiry Check Service stopped");
    }

    private async Task CheckCertificateExpiryAsync(CancellationToken ct)
    {
        var tlsConfigService = _serviceProvider.GetRequiredService<ITlsConfigService>();
        var notificationService = _serviceProvider.GetRequiredService<INotificationService>();

        var certInfo = await tlsConfigService.GetCertificateInfoAsync();
        if (certInfo == null)
        {
            _logger.LogDebug("No certificate info available, skipping expiry check");
            return;
        }

        var daysRemaining = (int)(certInfo.ExpiresAt - DateTime.UtcNow).TotalDays;

        foreach (var threshold in Thresholds)
        {
            if (daysRemaining > threshold)
                continue;

            var dedupKey = $"{certInfo.Thumbprint}:{threshold}";
            var alreadyExists = await notificationService.ExistsAsync(
                NotificationType.CertificateExpiry, "threshold", dedupKey, ct);

            if (alreadyExists)
                continue;

            var notification = NotificationFactory.CreateCertificateExpiryNotification(
                certInfo.Subject, certInfo.Thumbprint, certInfo.ExpiresAt, daysRemaining);

            await notificationService.AddAsync(notification, ct);

            _logger.LogInformation(
                "Certificate expiry notification created: {Subject} expires in {Days} days (threshold: {Threshold}d)",
                certInfo.Subject, daysRemaining, threshold);

            // Only create one notification per check cycle (the most urgent threshold)
            break;
        }
    }
}
