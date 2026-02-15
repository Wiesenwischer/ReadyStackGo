using FluentAssertions;
using Moq;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.UnitTests.Configuration;

public class OnboardingStateServiceTests
{
    private readonly Mock<IConfigStore> _configStoreMock;
    private SystemConfig _systemConfig;

    public OnboardingStateServiceTests()
    {
        _configStoreMock = new Mock<IConfigStore>();
        _systemConfig = new SystemConfig();

        _configStoreMock
            .Setup(x => x.GetSystemConfigAsync())
            .ReturnsAsync(() => _systemConfig);

        _configStoreMock
            .Setup(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()))
            .Callback<SystemConfig>(config => _systemConfig = config)
            .Returns(Task.CompletedTask);
    }

    private OnboardingStateService CreateService() => new(_configStoreMock.Object);

    [Fact]
    public async Task IsDismissedAsync_WhenNotDismissed_ReturnsFalse()
    {
        // Arrange
        _systemConfig.OnboardingDismissed = false;
        var service = CreateService();

        // Act
        var result = await service.IsDismissedAsync();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsDismissedAsync_WhenDismissed_ReturnsTrue()
    {
        // Arrange
        _systemConfig.OnboardingDismissed = true;
        var service = CreateService();

        // Act
        var result = await service.IsDismissedAsync();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task DismissAsync_SetsOnboardingDismissedToTrue()
    {
        // Arrange
        _systemConfig.OnboardingDismissed = false;
        var service = CreateService();

        // Act
        await service.DismissAsync();

        // Assert
        _systemConfig.OnboardingDismissed.Should().BeTrue();
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Once);
    }

    [Fact]
    public async Task DismissAsync_WhenAlreadyDismissed_StillSaves()
    {
        // Arrange
        _systemConfig.OnboardingDismissed = true;
        var service = CreateService();

        // Act
        await service.DismissAsync();

        // Assert
        _systemConfig.OnboardingDismissed.Should().BeTrue();
        _configStoreMock.Verify(x => x.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Once);
    }

    [Fact]
    public async Task IsDismissedAsync_DefaultConfig_ReturnsFalse()
    {
        // Arrange - fresh SystemConfig with defaults
        var service = CreateService();

        // Act
        var result = await service.IsDismissedAsync();

        // Assert
        result.Should().BeFalse();
    }
}
