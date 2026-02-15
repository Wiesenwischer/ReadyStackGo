using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.UnitTests.Configuration;

public class WizardTimeoutServiceTests
{
    private readonly Mock<IConfigStore> _configStoreMock;
    private readonly Mock<ILogger<WizardTimeoutService>> _loggerMock;
    private readonly IConfiguration _configuration;
    private SystemConfig _systemConfig;

    public WizardTimeoutServiceTests()
    {
        // Reset static flag before each test
        WizardTimeoutService.ResetStartupFlag();

        _configStoreMock = new Mock<IConfigStore>();
        _loggerMock = new Mock<ILogger<WizardTimeoutService>>();
        _systemConfig = new SystemConfig();

        // Default configuration with 5-minute (300 seconds) timeout
        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wizard:TimeoutSeconds"] = "300"
            })
            .Build();

        _configStoreMock
            .Setup(x => x.GetSystemConfigAsync())
            .ReturnsAsync(() => _systemConfig);

        _configStoreMock
            .Setup(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()))
            .Callback<SystemConfig>(config => _systemConfig = config)
            .Returns(Task.CompletedTask);
    }

    private WizardTimeoutService CreateService() =>
        new(_configStoreMock.Object, _configuration, _loggerMock.Object);

    [Fact]
    public async Task GetTimeoutInfoAsync_WhenWizardCompleted_ReturnsNoTimeout()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.Installed;
        var service = CreateService();

        // Act
        var result = await service.GetTimeoutInfoAsync();

        // Assert
        result.IsTimedOut.Should().BeFalse();
        result.IsLocked.Should().BeFalse();
        result.StartedAt.Should().BeNull();
        result.ExpiresAt.Should().BeNull();
        result.RemainingSeconds.Should().BeNull();
        result.TimeoutSeconds.Should().Be(300);
    }

    [Fact]
    public async Task GetTimeoutInfoAsync_WhenFirstAccess_InitializesTimeoutWindow()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = null;
        var service = CreateService();

        // Act
        var result = await service.GetTimeoutInfoAsync();

        // Assert
        result.IsTimedOut.Should().BeFalse();
        result.IsLocked.Should().BeFalse();
        result.StartedAt.Should().NotBeNull();
        result.StartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        result.ExpiresAt.Should().NotBeNull();
        result.RemainingSeconds.Should().BeGreaterThan(295); // Should be close to 300 seconds
        result.TimeoutSeconds.Should().Be(300);

        // Verify config was saved
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Once);
        _systemConfig.WizardStartedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetTimeoutInfoAsync_WhenAlreadyStarted_ReturnsExistingTimeout()
    {
        // Arrange
        var startedAt = DateTime.UtcNow.AddMinutes(-2); // Started 2 minutes ago
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = startedAt;
        var service = CreateService();

        // Act
        var result = await service.GetTimeoutInfoAsync();

        // Assert
        result.IsTimedOut.Should().BeFalse();
        result.IsLocked.Should().BeFalse();
        result.StartedAt.Should().Be(startedAt);
        result.RemainingSeconds.Should().BeInRange(170, 185); // About 3 minutes remaining
    }

    [Fact]
    public async Task GetTimeoutInfoAsync_WhenTimeoutExpired_ReturnsTimedOutAndSetsLock()
    {
        // Arrange
        var startedAt = DateTime.UtcNow.AddMinutes(-10); // Started 10 minutes ago
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = startedAt;
        var service = CreateService();

        // Act
        var result = await service.GetTimeoutInfoAsync();

        // Assert
        result.IsTimedOut.Should().BeTrue();
        result.IsLocked.Should().BeTrue();
        result.RemainingSeconds.Should().BeNull();
        _systemConfig.IsWizardLocked.Should().BeTrue();
    }

    [Fact]
    public async Task GetTimeoutInfoAsync_WhenLocked_ReturnsLockedState()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.IsWizardLocked = true;
        _systemConfig.WizardStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var service = CreateService();

        // Act
        var result = await service.GetTimeoutInfoAsync();

        // Assert
        result.IsTimedOut.Should().BeTrue();
        result.IsLocked.Should().BeTrue();
        result.RemainingSeconds.Should().BeNull();
    }

    [Fact]
    public async Task IsTimedOutAsync_WhenNotExpired_ReturnsFalse()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = DateTime.UtcNow;
        var service = CreateService();

        // Act
        var result = await service.IsTimedOutAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsTimedOutAsync_WhenExpired_ReturnsTrue()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var service = CreateService();

        // Act
        var result = await service.IsTimedOutAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsLockedAsync_WhenLocked_ReturnsTrue()
    {
        // Arrange
        _systemConfig.IsWizardLocked = true;
        var service = CreateService();

        // Act
        var result = await service.IsLockedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsLockedAsync_WhenNotLocked_ReturnsFalse()
    {
        // Arrange
        _systemConfig.IsWizardLocked = false;
        var service = CreateService();

        // Act
        var result = await service.IsLockedAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task ResetTimeoutAsync_WhenLocked_DoesNotReset()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.IsWizardLocked = true;
        _systemConfig.WizardStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var service = CreateService();

        // Act
        await service.ResetTimeoutAsync();

        // Assert - lock cannot be reset via API
        _systemConfig.IsWizardLocked.Should().BeTrue();
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Never);
    }

    [Fact]
    public async Task ResetTimeoutAsync_WhenNotLocked_ResetsPartialState()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.IsWizardLocked = false;
        _systemConfig.WizardStartedAt = DateTime.UtcNow;
        var service = CreateService();

        // Act
        await service.ResetTimeoutAsync();

        // Assert - only wizard state is reset, not the lock
        _systemConfig.WizardState.Should().Be(WizardState.NotStarted);
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Once);
    }

    [Fact]
    public async Task ResetTimeoutAsync_WhenWizardCompleted_DoesNotReset()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.Installed;
        _systemConfig.WizardStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var service = CreateService();

        // Act
        await service.ResetTimeoutAsync();

        // Assert
        _systemConfig.WizardState.Should().Be(WizardState.Installed);
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Never);
    }

    [Fact]
    public async Task ClearTimeoutAsync_ClearsTimestampAndLock()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.Installed;
        _systemConfig.WizardStartedAt = DateTime.UtcNow;
        _systemConfig.IsWizardLocked = true;
        var service = CreateService();

        // Act
        await service.ClearTimeoutAsync();

        // Assert
        _systemConfig.WizardStartedAt.Should().BeNull();
        _systemConfig.IsWizardLocked.Should().BeFalse();
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Once);
    }

    [Fact]
    public async Task ClearTimeoutAsync_WhenNoTimestamp_DoesNotSave()
    {
        // Arrange
        _systemConfig.WizardStartedAt = null;
        _systemConfig.IsWizardLocked = false;
        var service = CreateService();

        // Act
        await service.ClearTimeoutAsync();

        // Assert
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Never);
    }

    [Fact]
    public async Task GetTimeoutInfoAsync_WithCustomTimeoutConfiguration_UsesConfiguredValue()
    {
        // Arrange
        var customConfig = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Wizard:TimeoutSeconds"] = "60" // 1 minute
            })
            .Build();

        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = DateTime.UtcNow;
        var service = new WizardTimeoutService(_configStoreMock.Object, customConfig, _loggerMock.Object);

        // Act
        var result = await service.GetTimeoutInfoAsync();

        // Assert
        result.TimeoutSeconds.Should().Be(60);
        result.RemainingSeconds.Should().BeLessThanOrEqualTo(60);
    }

    [Fact]
    public async Task InitializeOnStartupAsync_WhenWizardCompleted_DoesNotInitialize()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.Installed;
        var service = CreateService();

        // Act
        await service.InitializeOnStartupAsync();

        // Assert
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Never);
    }

    [Fact]
    public async Task InitializeOnStartupAsync_WhenNotCompleted_InitializesTimeout()
    {
        // Arrange
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.WizardStartedAt = null;
        var service = CreateService();

        // Act
        await service.InitializeOnStartupAsync();

        // Assert
        _systemConfig.WizardStartedAt.Should().NotBeNull();
        _systemConfig.WizardStartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        _systemConfig.IsWizardLocked.Should().BeFalse();
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Once);
    }

    [Fact]
    public async Task InitializeOnStartupAsync_ClearsPreviousLock()
    {
        // Arrange - simulates container restart after previous timeout
        _systemConfig.WizardState = WizardState.NotStarted;
        _systemConfig.IsWizardLocked = true;
        _systemConfig.WizardStartedAt = DateTime.UtcNow.AddMinutes(-10);
        var service = CreateService();

        // Act
        await service.InitializeOnStartupAsync();

        // Assert - container restart clears the lock
        _systemConfig.IsWizardLocked.Should().BeFalse();
        _systemConfig.WizardStartedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }
}
