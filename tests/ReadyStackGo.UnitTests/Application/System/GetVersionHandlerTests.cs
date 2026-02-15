using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Notifications;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.System.GetVersion;

namespace ReadyStackGo.UnitTests.Application.System;

/// <summary>
/// Unit tests for GetVersionHandler.
/// Tests version comparison logic and response building.
/// </summary>
public class GetVersionHandlerTests
{
    private readonly Mock<IVersionCheckService> _versionCheckServiceMock;
    private readonly Mock<ILogger<GetVersionHandler>> _loggerMock;
    private readonly GetVersionHandler _handler;

    public GetVersionHandlerTests()
    {
        _versionCheckServiceMock = new Mock<IVersionCheckService>();
        _loggerMock = new Mock<ILogger<GetVersionHandler>>();
        _handler = new GetVersionHandler(_versionCheckServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_ReturnsCurrentVersion()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LatestVersionInfo?)null);

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.ServerVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_NoLatestVersion_UpdateAvailableIsFalse()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LatestVersionInfo?)null);

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.UpdateAvailable.Should().BeFalse();
        result.LatestVersion.Should().BeNull();
        result.LatestReleaseUrl.Should().BeNull();
    }

    [Fact]
    public async Task Handle_NewerVersionAvailable_UpdateAvailableIsTrue()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.UpdateAvailable.Should().BeTrue();
        result.LatestVersion.Should().Be("2.0.0");
        result.LatestReleaseUrl.Should().Be("https://github.com/release");
    }

    [Fact]
    public async Task Handle_OlderVersionOnGitHub_UpdateAvailableIsFalse()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("2.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("1.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.UpdateAvailable.Should().BeFalse();
        result.LatestVersion.Should().Be("1.0.0");
    }

    [Fact]
    public async Task Handle_SameVersion_UpdateAvailableIsFalse()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("1.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.UpdateAvailable.Should().BeFalse();
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]  // Patch update
    [InlineData("1.0.0", "1.1.0", true)]  // Minor update
    [InlineData("1.0.0", "2.0.0", true)]  // Major update
    [InlineData("1.2.3", "1.2.3", false)] // Same version
    [InlineData("2.0.0", "1.9.9", false)] // Downgrade
    [InlineData("1.10.0", "1.9.0", false)] // Higher minor (10 > 9)
    public async Task Handle_VersionComparison_WorksCorrectly(string current, string latest, bool expectUpdate)
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns(current);
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo(latest, "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.UpdateAvailable.Should().Be(expectUpdate,
            $"expected update={expectUpdate} for current={current} vs latest={latest}");
    }

    [Fact]
    public async Task Handle_VersionWithVPrefix_ComparesCorrectly()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("v1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("v2.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.UpdateAvailable.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_IncludesBuildInfo()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LatestVersionInfo?)null);

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.Build.Should().NotBeNull();
        result.Build.RuntimeVersion.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Handle_ForceCheck_PassesParameterToService()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://url", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        await _handler.Handle(new GetVersionQuery(ForceCheck: true), CancellationToken.None);

        // Assert
        _versionCheckServiceMock.Verify(
            s => s.GetLatestVersionAsync(true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_IncludesCheckedAtFromLatestInfo()
    {
        // Arrange
        var checkedAt = new DateTime(2026, 2, 15, 10, 0, 0, DateTimeKind.Utc);
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://url", DateTime.UtcNow, checkedAt));

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.CheckedAt.Should().Be(checkedAt);
    }

    [Fact]
    public async Task Handle_NoLatestInfo_CheckedAtIsNull()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((LatestVersionInfo?)null);

        // Act
        var result = await _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        result.CheckedAt.Should().BeNull();
    }

    // --- Notification Tests ---

    [Fact]
    public async Task Handle_UpdateAvailable_CreatesNotification()
    {
        // Arrange
        var notificationMock = new Mock<INotificationService>();
        notificationMock.Setup(n => n.ExistsAsync(
            NotificationType.UpdateAvailable, "latestVersion", "2.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new GetVersionHandler(
            _versionCheckServiceMock.Object, _loggerMock.Object, notificationMock.Object);

        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        await handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        notificationMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif =>
                notif.Type == NotificationType.UpdateAvailable &&
                notif.Severity == NotificationSeverity.Info &&
                notif.Title == "Update Available" &&
                notif.Message.Contains("2.0.0") &&
                notif.ActionUrl == "/settings/system" &&
                notif.Metadata["latestVersion"] == "2.0.0"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_UpdateAvailable_DeduplicatesByVersion()
    {
        // Arrange
        var notificationMock = new Mock<INotificationService>();
        notificationMock.Setup(n => n.ExistsAsync(
            NotificationType.UpdateAvailable, "latestVersion", "2.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true); // Already exists

        var handler = new GetVersionHandler(
            _versionCheckServiceMock.Object, _loggerMock.Object, notificationMock.Object);

        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        await handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        notificationMock.Verify(n => n.AddAsync(
            It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NoUpdate_DoesNotCreateNotification()
    {
        // Arrange
        var notificationMock = new Mock<INotificationService>();
        var handler = new GetVersionHandler(
            _versionCheckServiceMock.Object, _loggerMock.Object, notificationMock.Object);

        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("2.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("1.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        await handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        notificationMock.Verify(n => n.AddAsync(
            It.IsAny<Notification>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task Handle_NullNotificationService_DoesNotThrow()
    {
        // Arrange — handler without INotificationService (null)
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://github.com/release", DateTime.UtcNow, DateTime.UtcNow));

        // Act — _handler was created without notification service
        var act = () => _handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task Handle_UpdateAvailable_IncludesReleaseUrlInMetadata()
    {
        // Arrange
        var notificationMock = new Mock<INotificationService>();
        notificationMock.Setup(n => n.ExistsAsync(
            It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var handler = new GetVersionHandler(
            _versionCheckServiceMock.Object, _loggerMock.Object, notificationMock.Object);

        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _versionCheckServiceMock.Setup(s => s.GetLatestVersionAsync(It.IsAny<bool>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LatestVersionInfo("2.0.0", "https://github.com/release/v2", DateTime.UtcNow, DateTime.UtcNow));

        // Act
        await handler.Handle(new GetVersionQuery(), CancellationToken.None);

        // Assert
        notificationMock.Verify(n => n.AddAsync(
            It.Is<Notification>(notif =>
                notif.Metadata.ContainsKey("releaseUrl") &&
                notif.Metadata["releaseUrl"] == "https://github.com/release/v2"),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
