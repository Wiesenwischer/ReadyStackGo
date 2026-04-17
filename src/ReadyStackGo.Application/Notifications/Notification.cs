namespace ReadyStackGo.Application.Notifications;

/// <summary>
/// Represents an in-app notification. This is a simple POCO, not a DDD aggregate —
/// notifications are transient, non-domain-critical data.
/// </summary>
public class Notification
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public NotificationSeverity Severity { get; set; } = NotificationSeverity.Info;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? ActionUrl { get; set; }
    public string? ActionLabel { get; set; }
    public Dictionary<string, string> Metadata { get; set; } = new();
}

public enum NotificationType
{
    UpdateAvailable,
    SourceSyncResult,
    DeploymentResult,
    ProductDeploymentResult,
    HealthChange,
    ApiKeyFirstUse,
    CertificateExpiry
}

public enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error
}
