using ReadyStackGo.Application.Notifications;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for managing in-app notifications.
/// </summary>
public interface INotificationService
{
    Task AddAsync(Notification notification, CancellationToken ct = default);
    Task<IReadOnlyList<Notification>> GetAllAsync(CancellationToken ct = default);
    Task<int> GetUnreadCountAsync(CancellationToken ct = default);
    Task MarkAsReadAsync(string id, CancellationToken ct = default);
    Task MarkAllAsReadAsync(CancellationToken ct = default);
    Task DismissAsync(string id, CancellationToken ct = default);

    /// <summary>
    /// Checks whether a notification with the given type and metadata key/value already exists.
    /// Used for deduplication (e.g. one notification per version for update-available).
    /// </summary>
    Task<bool> ExistsAsync(NotificationType type, string metadataKey, string metadataValue, CancellationToken ct = default);
}
