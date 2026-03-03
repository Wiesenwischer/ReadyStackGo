using FluentAssertions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.UseCases.System.GetHealthNotificationSettings;
using ReadyStackGo.Application.UseCases.System.UpdateHealthNotificationSettings;

namespace ReadyStackGo.UnitTests.UseCases.System;

public class HealthNotificationSettingsTests
{
    private readonly Mock<ISystemConfigService> _systemConfigService = new();

    // --- Get ---

    [Fact]
    public async Task GetHandler_ReturnsCurrentCooldown()
    {
        _systemConfigService
            .Setup(s => s.GetHealthNotificationCooldownSecondsAsync())
            .ReturnsAsync(300);

        var handler = new GetHealthNotificationSettingsHandler(_systemConfigService.Object);
        var result = await handler.Handle(new GetHealthNotificationSettingsQuery(), CancellationToken.None);

        result.CooldownSeconds.Should().Be(300);
    }

    [Fact]
    public async Task GetHandler_ReturnsCustomCooldown()
    {
        _systemConfigService
            .Setup(s => s.GetHealthNotificationCooldownSecondsAsync())
            .ReturnsAsync(600);

        var handler = new GetHealthNotificationSettingsHandler(_systemConfigService.Object);
        var result = await handler.Handle(new GetHealthNotificationSettingsQuery(), CancellationToken.None);

        result.CooldownSeconds.Should().Be(600);
    }

    // --- Update ---

    [Fact]
    public async Task UpdateHandler_ValidCooldown_SavesAndReturnsSuccess()
    {
        var handler = new UpdateHealthNotificationSettingsHandler(_systemConfigService.Object);
        var command = new UpdateHealthNotificationSettingsCommand { CooldownSeconds = 120 };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        _systemConfigService.Verify(
            s => s.SetHealthNotificationCooldownSecondsAsync(120),
            Times.Once);
    }

    [Fact]
    public async Task UpdateHandler_TooLow_ReturnsError()
    {
        var handler = new UpdateHealthNotificationSettingsHandler(_systemConfigService.Object);
        var command = new UpdateHealthNotificationSettingsCommand { CooldownSeconds = 30 };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("between 60 and 3600");
        _systemConfigService.Verify(
            s => s.SetHealthNotificationCooldownSecondsAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateHandler_TooHigh_ReturnsError()
    {
        var handler = new UpdateHealthNotificationSettingsHandler(_systemConfigService.Object);
        var command = new UpdateHealthNotificationSettingsCommand { CooldownSeconds = 7200 };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeFalse();
        _systemConfigService.Verify(
            s => s.SetHealthNotificationCooldownSecondsAsync(It.IsAny<int>()),
            Times.Never);
    }

    [Theory]
    [InlineData(60)]
    [InlineData(300)]
    [InlineData(3600)]
    public async Task UpdateHandler_BoundaryValues_Accepted(int cooldown)
    {
        var handler = new UpdateHealthNotificationSettingsHandler(_systemConfigService.Object);
        var command = new UpdateHealthNotificationSettingsCommand { CooldownSeconds = cooldown };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
    }
}
