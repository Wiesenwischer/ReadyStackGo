using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Infrastructure.Configuration;
using ReadyStackGo.Infrastructure.Services.Health;

namespace ReadyStackGo.UnitTests.Notifications;

public class HealthChangeTrackerTests
{
    private readonly Mock<INotificationService> _notificationService = new();
    private readonly Mock<IConfigStore> _configStore = new();
    private readonly HealthChangeTracker _tracker;

    public HealthChangeTrackerTests()
    {
        var config = new SystemConfig { HealthNotificationCooldownSeconds = 300 };
        _configStore.Setup(c => c.GetSystemConfigAsync()).ReturnsAsync(config);

        _notificationService
            .Setup(n => n.ExistsAsync(It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _tracker = new HealthChangeTracker(
            _notificationService.Object,
            _configStore.Object,
            Mock.Of<ILogger<HealthChangeTracker>>());
    }

    [Fact]
    public async Task FirstCollection_NoNotification()
    {
        var statuses = new List<ServiceHealthUpdate>
        {
            new("api", "Healthy")
        };

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app", statuses);

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task HealthyToUnhealthy_CreatesErrorNotification()
    {
        // First collection: establish baseline
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Second collection: status changed
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        _notificationService.Verify(n => n.AddAsync(
            It.Is<Notification>(not =>
                not.Type == NotificationType.HealthChange &&
                not.Severity == NotificationSeverity.Error &&
                not.Metadata["currentStatus"] == "Unhealthy"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task UnhealthyToHealthy_CreatesInfoRecoveryNotification()
    {
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        _notificationService.Verify(n => n.AddAsync(
            It.Is<Notification>(not =>
                not.Type == NotificationType.HealthChange &&
                not.Severity == NotificationSeverity.Info &&
                not.Title == "Service Recovered"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task HealthyToDegraded_CreatesWarningNotification()
    {
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Degraded") });

        _notificationService.Verify(n => n.AddAsync(
            It.Is<Notification>(not =>
                not.Severity == NotificationSeverity.Warning),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task NoStatusChange_NoNotification()
    {
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task SameDegradationWithinCooldown_Throttled()
    {
        // Long cooldown — all subsequent changes should be throttled after the first
        var config = new SystemConfig { HealthNotificationCooldownSeconds = 600 };
        _configStore.Setup(c => c.GetSystemConfigAsync()).ReturnsAsync(config);

        // Establish baseline
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // First change: Healthy → Unhealthy (creates notification, starts cooldown)
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        // Recovery within cooldown: Unhealthy → Healthy (throttled)
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Second degradation within cooldown: Healthy → Unhealthy (throttled)
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        // Only 1 notification total — subsequent changes within cooldown are throttled
        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DifferentServicesTrackedIndependently()
    {
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate>
            {
                new("api", "Healthy"),
                new("worker", "Healthy")
            });

        // Only api changes, worker stays the same
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate>
            {
                new("api", "Unhealthy"),
                new("worker", "Healthy")
            });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task DifferentDeploymentsTrackedIndependently()
    {
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-2", "backend",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Both change at same time
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-2", "backend",
            new List<ServiceHealthUpdate> { new("api", "Degraded") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task ExistsAsyncTrue_SkipsNotification()
    {
        _notificationService
            .Setup(n => n.ExistsAsync(NotificationType.HealthChange, "serviceKey", "dep-1:api", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task NotificationServiceFails_NoException()
    {
        _notificationService
            .Setup(n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Service unavailable"));

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Should not throw
        var act = () => _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ResetBaseline_NextTransitionIsTreatedAsFirstSeen_NoNotification()
    {
        // Establish baseline
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Deployment enters Installing/Upgrading — reset baseline
        await _tracker.ResetBaselineAsync("dep-1");

        // Post-recovery first cycle: current="Starting", previous=null (cleared) → no notif
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Starting") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Genuine later transition — fires normally
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetBaseline_OnlyAffectsTargetDeployment()
    {
        // Establish baselines for two deployments
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });
        await _tracker.ProcessHealthUpdateAsync("dep-2", "backend",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Reset only dep-1
        await _tracker.ResetBaselineAsync("dep-1");

        // dep-1 transition: treated as first-seen → no notif
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        // dep-2 transition: baseline intact → notif fires
        await _tracker.ProcessHealthUpdateAsync("dep-2", "backend",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ResetBaseline_UnknownDeployment_ShouldNotThrow()
    {
        var act = () => _tracker.ResetBaselineAsync("never-seen");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task SuppressNotifications_SkipsNotification_ButKeepsBaselineForLaterTransitions()
    {
        // Baseline
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        // Status change with suppressNotifications=true — no notif, baseline advances to Starting
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Starting") },
            suppressNotifications: true);

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Follow-up with same status as suppressed baseline — no transition, no notif
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Starting") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);

        // Real transition after suppression ends — fires normally
        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task CooldownReadFromConfig()
    {
        // Very short cooldown
        var config = new SystemConfig { HealthNotificationCooldownSeconds = 0 };
        _configStore.Setup(c => c.GetSystemConfigAsync()).ReturnsAsync(config);

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Healthy") });

        await _tracker.ProcessHealthUpdateAsync("dep-1", "web-app",
            new List<ServiceHealthUpdate> { new("api", "Unhealthy") });

        // With cooldown=0, all changes should create notifications
        _notificationService.Verify(
            n => n.AddAsync(It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Exactly(3)); // Unhealthy + Healthy(recovery) + Unhealthy
    }
}
