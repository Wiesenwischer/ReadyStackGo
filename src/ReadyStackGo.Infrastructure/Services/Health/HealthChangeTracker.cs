using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.Infrastructure.Services.Health;

/// <summary>
/// Singleton service that tracks health status changes across collection cycles
/// and creates in-app notifications with configurable cooldown throttling.
/// </summary>
public class HealthChangeTracker : IHealthChangeTracker
{
    private readonly INotificationService _notificationService;
    private readonly IConfigStore _configStore;
    private readonly ILogger<HealthChangeTracker> _logger;

    private readonly ConcurrentDictionary<string, string> _previousStatuses = new();
    private readonly ConcurrentDictionary<string, DateTime> _cooldownTimestamps = new();

    public HealthChangeTracker(
        INotificationService notificationService,
        IConfigStore configStore,
        ILogger<HealthChangeTracker> logger)
    {
        _notificationService = notificationService;
        _configStore = configStore;
        _logger = logger;
    }

    public async Task ProcessHealthUpdateAsync(
        string deploymentId,
        string stackName,
        IReadOnlyList<ServiceHealthUpdate> serviceStatuses,
        bool suppressNotifications = false,
        CancellationToken ct = default)
    {
        var config = await _configStore.GetSystemConfigAsync();
        var cooldownSeconds = config.HealthNotificationCooldownSeconds;

        foreach (var service in serviceStatuses)
        {
            var serviceKey = $"{deploymentId}:{service.ServiceName}";
            var currentStatus = service.Status;

            var previousStatus = _previousStatuses.GetValueOrDefault(serviceKey);
            _previousStatuses[serviceKey] = currentStatus;

            // No previous status (first collection) or no change — skip
            if (previousStatus == null || string.Equals(previousStatus, currentStatus, StringComparison.OrdinalIgnoreCase))
                continue;

            // Deployment currently installing/upgrading — don't emit health-change noise;
            // the final ProductDeploymentResult notification is the authoritative signal.
            if (suppressNotifications)
                continue;

            // Status changed — check cooldown
            if (!IsOutsideCooldown(serviceKey, cooldownSeconds))
            {
                _logger.LogDebug(
                    "Health notification throttled for {ServiceKey} (cooldown {Cooldown}s)",
                    serviceKey, cooldownSeconds);
                continue;
            }

            // Check dedup via notification store
            try
            {
                var alreadyExists = await _notificationService.ExistsAsync(
                    NotificationType.HealthChange, "serviceKey", serviceKey, ct);

                if (alreadyExists)
                {
                    _logger.LogDebug("Health notification deduplicated for {ServiceKey}", serviceKey);
                    continue;
                }

                var notification = NotificationFactory.CreateHealthChangeNotification(
                    stackName, service.ServiceName,
                    previousStatus, currentStatus,
                    deploymentId);

                await _notificationService.AddAsync(notification, ct);
                _cooldownTimestamps[serviceKey] = DateTime.UtcNow;

                _logger.LogInformation(
                    "Health notification created: {ServiceName} in {StackName} changed from {Previous} to {Current}",
                    service.ServiceName, stackName, previousStatus, currentStatus);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Failed to create health change notification for {ServiceKey}", serviceKey);
            }
        }
    }

    public Task ResetBaselineAsync(string deploymentId, CancellationToken ct = default)
    {
        var prefix = $"{deploymentId}:";
        var removed = 0;

        foreach (var key in _previousStatuses.Keys.Where(k => k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList())
        {
            if (_previousStatuses.TryRemove(key, out _))
                removed++;
            _cooldownTimestamps.TryRemove(key, out _);
        }

        if (removed > 0)
            _logger.LogDebug("Reset health baseline for deployment {DeploymentId} ({Count} entries)", deploymentId, removed);

        return Task.CompletedTask;
    }

    private bool IsOutsideCooldown(string serviceKey, int cooldownSeconds)
    {
        if (!_cooldownTimestamps.TryGetValue(serviceKey, out var lastNotificationTime))
            return true;

        return (DateTime.UtcNow - lastNotificationTime).TotalSeconds >= cooldownSeconds;
    }
}
