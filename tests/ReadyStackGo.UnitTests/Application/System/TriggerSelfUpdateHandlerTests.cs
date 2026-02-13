using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.System.TriggerSelfUpdate;

namespace ReadyStackGo.UnitTests.Application.System;

/// <summary>
/// Unit tests for TriggerSelfUpdateHandler.
/// Tests version validation, delegation to ISelfUpdateService, and error handling.
/// </summary>
public class TriggerSelfUpdateHandlerTests
{
    private readonly Mock<ISelfUpdateService> _selfUpdateServiceMock;
    private readonly Mock<IVersionCheckService> _versionCheckServiceMock;
    private readonly Mock<ILogger<TriggerSelfUpdateHandler>> _loggerMock;
    private readonly TriggerSelfUpdateHandler _handler;

    public TriggerSelfUpdateHandlerTests()
    {
        _selfUpdateServiceMock = new Mock<ISelfUpdateService>();
        _versionCheckServiceMock = new Mock<IVersionCheckService>();
        _loggerMock = new Mock<ILogger<TriggerSelfUpdateHandler>>();
        _handler = new TriggerSelfUpdateHandler(
            _selfUpdateServiceMock.Object,
            _versionCheckServiceMock.Object,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Handle_EmptyTargetVersion_ReturnsFailure()
    {
        // Arrange
        var command = new TriggerSelfUpdateCommand("");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Target version must be specified");
        _selfUpdateServiceMock.Verify(
            s => s.TriggerUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhitespaceTargetVersion_ReturnsFailure()
    {
        // Arrange
        var command = new TriggerSelfUpdateCommand("   ");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Target version must be specified");
    }

    [Fact]
    public async Task Handle_SameVersion_ReturnsFailure()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        var command = new TriggerSelfUpdateCommand("1.0.0");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not newer");
        _selfUpdateServiceMock.Verify(
            s => s.TriggerUpdateAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_OlderVersion_ReturnsFailure()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("2.0.0");
        var command = new TriggerSelfUpdateCommand("1.0.0");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("not newer");
    }

    [Fact]
    public async Task Handle_NewerVersion_DelegatesToSelfUpdateService()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _selfUpdateServiceMock
            .Setup(s => s.TriggerUpdateAsync("2.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfUpdateResult(true, "Update initiated"));

        var command = new TriggerSelfUpdateCommand("2.0.0");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
        result.Message.Should().Contain("Update initiated");
        _selfUpdateServiceMock.Verify(
            s => s.TriggerUpdateAsync("2.0.0", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_ServiceReturnsFailure_PropagatesFailure()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _selfUpdateServiceMock
            .Setup(s => s.TriggerUpdateAsync("2.0.0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfUpdateResult(false, "Docker error: connection refused"));

        var command = new TriggerSelfUpdateCommand("2.0.0");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Docker error");
    }

    [Fact]
    public async Task Handle_PatchUpdate_Succeeds()
    {
        // Arrange
        _versionCheckServiceMock.Setup(s => s.GetCurrentVersion()).Returns("1.0.0");
        _selfUpdateServiceMock
            .Setup(s => s.TriggerUpdateAsync("1.0.1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SelfUpdateResult(true, "Update initiated"));

        var command = new TriggerSelfUpdateCommand("1.0.1");

        // Act
        var result = await _handler.Handle(command, CancellationToken.None);

        // Assert
        result.Success.Should().BeTrue();
    }

    [Theory]
    [InlineData("1.0.0", "1.0.1", true)]   // Patch update
    [InlineData("1.0.0", "1.1.0", true)]   // Minor update
    [InlineData("1.0.0", "2.0.0", true)]   // Major update
    [InlineData("1.2.3", "1.2.3", false)]  // Same version
    [InlineData("2.0.0", "1.9.9", false)]  // Downgrade
    [InlineData("1.10.0", "1.9.0", false)] // 10 > 9 (numeric, not string comparison)
    [InlineData("0.19.0", "0.20.0", true)] // Typical RSGO version update
    public void IsNewerVersion_WorksCorrectly(string current, string target, bool expected)
    {
        // Act
        var result = TriggerSelfUpdateHandler.IsNewerVersion(current, target);

        // Assert
        result.Should().Be(expected,
            $"expected IsNewerVersion({current}, {target}) = {expected}");
    }

    [Theory]
    [InlineData("v1.0.0", "v2.0.0", true)]
    [InlineData("v2.0.0", "v1.0.0", false)]
    [InlineData("V1.0.0", "v1.1.0", true)]
    public void IsNewerVersion_HandlesVPrefix(string current, string target, bool expected)
    {
        var result = TriggerSelfUpdateHandler.IsNewerVersion(current, target);
        result.Should().Be(expected);
    }
}
