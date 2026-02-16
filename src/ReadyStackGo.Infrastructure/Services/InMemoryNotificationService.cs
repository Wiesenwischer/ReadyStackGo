using System.Collections.Concurrent;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Services;

/// <summary>
/// In-memory notification store with FIFO eviction at 50 entries.
/// Registered as a singleton — notifications are transient pre-v1.0.
/// </summary>
public class InMemoryNotificationService : INotificationService
{
    internal const int MaxNotifications = 50;

    private readonly ConcurrentDictionary<string, Notification> _notifications = new();
    private readonly object _evictionLock = new();

    public Task AddAsync(Notification notification, CancellationToken ct = default)
    {
        lock (_evictionLock)
        {
            _notifications[notification.Id] = notification;
            EvictOldest();
        }

        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken ct = default)
    {
        var result = _notifications.Values
            .OrderByDescending(n => n.CreatedAt)
            .ToList();

        return Task.FromResult<IReadOnlyList<Notification>>(result);
    }

    public Task<int> GetUnreadCountAsync(CancellationToken ct = default)
    {
        var count = _notifications.Values.Count(n => !n.Read);
        return Task.FromResult(count);
    }

    public Task MarkAsReadAsync(string id, CancellationToken ct = default)
    {
        if (_notifications.TryGetValue(id, out var notification))
        {
            notification.Read = true;
        }

        return Task.CompletedTask;
    }

    public Task MarkAllAsReadAsync(CancellationToken ct = default)
    {
        foreach (var notification in _notifications.Values)
        {
            notification.Read = true;
        }

        return Task.CompletedTask;
    }

    public Task DismissAsync(string id, CancellationToken ct = default)
    {
        _notifications.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task<bool> ExistsAsync(NotificationType type, string metadataKey, string metadataValue,
        CancellationToken ct = default)
    {
        var exists = _notifications.Values.Any(n =>
            n.Type == type &&
            n.Metadata.TryGetValue(metadataKey, out var value) &&
            value == metadataValue);

        return Task.FromResult(exists);
    }

    private void EvictOldest()
    {
        while (_notifications.Count > MaxNotifications)
        {
            var oldest = _notifications.Values
                .OrderBy(n => n.CreatedAt)
                .FirstOrDefault();

            if (oldest != null)
            {
                _notifications.TryRemove(oldest.Id, out _);
            }
        }
    }
}
