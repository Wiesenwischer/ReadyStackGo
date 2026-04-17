using FluentAssertions;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Infrastructure.Services;

namespace ReadyStackGo.UnitTests.Services;

public class InMemoryNotificationServiceTests
{
    private InMemoryNotificationService CreateService() => new();

    private static Notification CreateNotification(
        NotificationType type = NotificationType.UpdateAvailable,
        NotificationSeverity severity = NotificationSeverity.Info,
        string? id = null,
        Dictionary<string, string>? metadata = null)
    {
        return new Notification
        {
            Id = id ?? Guid.NewGuid().ToString("N"),
            Type = type,
            Title = "Test Notification",
            Message = "Test message",
            Severity = severity,
            CreatedAt = DateTime.UtcNow,
            Metadata = metadata ?? new Dictionary<string, string>()
        };
    }

    [Fact]
    public async Task AddAsync_ShouldStoreNotification()
    {
        var sut = CreateService();
        var notification = CreateNotification();

        await sut.AddAsync(notification);

        var all = await sut.GetAllAsync();
        all.Should().ContainSingle().Which.Id.Should().Be(notification.Id);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnNewestFirst()
    {
        var sut = CreateService();

        var older = CreateNotification(id: "older");
        older.CreatedAt = DateTime.UtcNow.AddMinutes(-5);

        var newer = CreateNotification(id: "newer");
        newer.CreatedAt = DateTime.UtcNow;

        await sut.AddAsync(older);
        await sut.AddAsync(newer);

        var all = await sut.GetAllAsync();
        all.Should().HaveCount(2);
        all[0].Id.Should().Be("newer");
        all[1].Id.Should().Be("older");
    }

    [Fact]
    public async Task GetAllAsync_EmptyStore_ShouldReturnEmptyList()
    {
        var sut = CreateService();
        var all = await sut.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task GetCountAsync_ShouldReturnTotal()
    {
        var sut = CreateService();

        await sut.AddAsync(CreateNotification());
        await sut.AddAsync(CreateNotification());
        await sut.AddAsync(CreateNotification());

        var count = await sut.GetCountAsync();
        count.Should().Be(3);
    }

    [Fact]
    public async Task GetCountAsync_EmptyStore_ShouldReturnZero()
    {
        var sut = CreateService();
        var count = await sut.GetCountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task DismissAsync_ShouldRemoveNotification()
    {
        var sut = CreateService();
        var notification = CreateNotification(id: "dismiss-me");
        await sut.AddAsync(notification);

        await sut.DismissAsync("dismiss-me");

        var all = await sut.GetAllAsync();
        all.Should().BeEmpty();
    }

    [Fact]
    public async Task DismissAsync_NonExistentId_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.DismissAsync("nonexistent");
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task DismissAsync_ShouldNotAffectOtherNotifications()
    {
        var sut = CreateService();
        await sut.AddAsync(CreateNotification(id: "keep"));
        await sut.AddAsync(CreateNotification(id: "remove"));

        await sut.DismissAsync("remove");

        var all = await sut.GetAllAsync();
        all.Should().ContainSingle().Which.Id.Should().Be("keep");
    }

    [Fact]
    public async Task DismissAllAsync_ShouldRemoveAll()
    {
        var sut = CreateService();
        await sut.AddAsync(CreateNotification());
        await sut.AddAsync(CreateNotification());
        await sut.AddAsync(CreateNotification());

        await sut.DismissAllAsync();

        var all = await sut.GetAllAsync();
        all.Should().BeEmpty();
        (await sut.GetCountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task DismissAllAsync_EmptyStore_ShouldNotThrow()
    {
        var sut = CreateService();
        var act = () => sut.DismissAllAsync();
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_ShouldFindByTypeAndMetadata()
    {
        var sut = CreateService();
        var notification = CreateNotification(
            type: NotificationType.UpdateAvailable,
            metadata: new Dictionary<string, string> { ["latestVersion"] = "1.0.0" });

        await sut.AddAsync(notification);

        var exists = await sut.ExistsAsync(NotificationType.UpdateAvailable, "latestVersion", "1.0.0");
        exists.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsAsync_DifferentType_ShouldReturnFalse()
    {
        var sut = CreateService();
        var notification = CreateNotification(
            type: NotificationType.UpdateAvailable,
            metadata: new Dictionary<string, string> { ["latestVersion"] = "1.0.0" });

        await sut.AddAsync(notification);

        var exists = await sut.ExistsAsync(NotificationType.SourceSyncResult, "latestVersion", "1.0.0");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_DifferentMetadataValue_ShouldReturnFalse()
    {
        var sut = CreateService();
        var notification = CreateNotification(
            type: NotificationType.UpdateAvailable,
            metadata: new Dictionary<string, string> { ["latestVersion"] = "1.0.0" });

        await sut.AddAsync(notification);

        var exists = await sut.ExistsAsync(NotificationType.UpdateAvailable, "latestVersion", "2.0.0");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_MissingMetadataKey_ShouldReturnFalse()
    {
        var sut = CreateService();
        var notification = CreateNotification(
            type: NotificationType.UpdateAvailable,
            metadata: new Dictionary<string, string> { ["otherKey"] = "1.0.0" });

        await sut.AddAsync(notification);

        var exists = await sut.ExistsAsync(NotificationType.UpdateAvailable, "latestVersion", "1.0.0");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_EmptyStore_ShouldReturnFalse()
    {
        var sut = CreateService();
        var exists = await sut.ExistsAsync(NotificationType.UpdateAvailable, "key", "value");
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task AddAsync_ShouldEvictOldestWhenExceedingMaxCapacity()
    {
        var sut = CreateService();
        var max = InMemoryNotificationService.MaxNotifications;

        for (int i = 0; i < max + 5; i++)
        {
            var n = CreateNotification(id: $"n-{i:D3}");
            n.CreatedAt = DateTime.UtcNow.AddMinutes(i);
            await sut.AddAsync(n);
        }

        var all = await sut.GetAllAsync();
        all.Should().HaveCount(max);

        all.Should().NotContain(n => n.Id == "n-000");
        all.Should().NotContain(n => n.Id == "n-004");

        all.Should().Contain(n => n.Id == $"n-{max + 4:D3}");
    }

    [Fact]
    public async Task AddAsync_AtExactlyMaxCapacity_ShouldNotEvict()
    {
        var sut = CreateService();
        var max = InMemoryNotificationService.MaxNotifications;

        for (int i = 0; i < max; i++)
        {
            await sut.AddAsync(CreateNotification());
        }

        var all = await sut.GetAllAsync();
        all.Should().HaveCount(max);
    }

    [Fact]
    public async Task AddAsync_DuplicateId_ShouldOverwrite()
    {
        var sut = CreateService();

        var original = CreateNotification(id: "same-id");
        original.Title = "Original";
        await sut.AddAsync(original);

        var updated = CreateNotification(id: "same-id");
        updated.Title = "Updated";
        await sut.AddAsync(updated);

        var all = await sut.GetAllAsync();
        all.Should().ContainSingle().Which.Title.Should().Be("Updated");
    }

    [Fact]
    public async Task ConcurrentAccess_ShouldNotThrow()
    {
        var sut = CreateService();

        var tasks = Enumerable.Range(0, 100).Select(async i =>
        {
            var notification = CreateNotification(id: $"concurrent-{i}");
            notification.CreatedAt = DateTime.UtcNow.AddMilliseconds(i);
            await sut.AddAsync(notification);

            if (i % 5 == 0)
                await sut.DismissAsync($"concurrent-{i}");

            if (i == 50)
                await sut.DismissAllAsync();

            await sut.GetCountAsync();
            await sut.GetAllAsync();
        });

        var act = () => Task.WhenAll(tasks);
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExistsAsync_AfterDismiss_ShouldReturnFalse()
    {
        var sut = CreateService();
        var notification = CreateNotification(
            type: NotificationType.UpdateAvailable,
            id: "dismiss-exists",
            metadata: new Dictionary<string, string> { ["latestVersion"] = "1.0.0" });

        await sut.AddAsync(notification);
        await sut.DismissAsync("dismiss-exists");

        var exists = await sut.ExistsAsync(NotificationType.UpdateAvailable, "latestVersion", "1.0.0");
        exists.Should().BeFalse();
    }
}
