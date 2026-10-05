using FluentAssertions;
using Moq;
using ReadyStackGo.Infrastructure.Configuration;

namespace ReadyStackGo.UnitTests.Infrastructure.Configuration;

public class SystemConfigServiceDefaultThemeTests
{
    private readonly Mock<IConfigStore> _store = new();
    private SystemConfig _config = new();

    public SystemConfigServiceDefaultThemeTests()
    {
        _store.Setup(s => s.GetSystemConfigAsync()).ReturnsAsync(() => _config);
        _store.Setup(s => s.SaveSystemConfigAsync(It.IsAny<SystemConfig>()))
            .Callback<SystemConfig>(c => _config = c)
            .Returns(Task.CompletedTask);
    }

    private SystemConfigService CreateService() => new(_store.Object);

    [Fact]
    public async Task GetDefaultTheme_NothingStored_ReturnsNull()
    {
        (await CreateService().GetDefaultThemeAsync()).Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task SetDefaultThemeIfUnset_Unset_StoresAndReturnsTrue(string? stored)
    {
        _config.DefaultTheme = stored;

        var result = await CreateService().SetDefaultThemeIfUnsetAsync("classic");

        result.Should().BeTrue();
        _config.DefaultTheme.Should().Be("classic");
        (await CreateService().GetDefaultThemeAsync()).Should().Be("classic");
    }

    [Fact]
    public async Task SetDefaultThemeIfUnset_AlreadyStored_KeepsValueAndDoesNotSave()
    {
        _config.DefaultTheme = "turquoise";

        var stored = await CreateService().SetDefaultThemeIfUnsetAsync("classic");

        stored.Should().BeFalse();
        _config.DefaultTheme.Should().Be("turquoise");
        _store.Verify(s => s.SaveSystemConfigAsync(It.IsAny<SystemConfig>()), Times.Never);
    }

    [Fact]
    public async Task SetDefaultThemeIfUnset_KeepsTheOtherSettings()
    {
        _config.WizardState = WizardState.Installed;
        _config.HealthNotificationCooldownSeconds = 42;

        await CreateService().SetDefaultThemeIfUnsetAsync("classic");

        _config.WizardState.Should().Be(WizardState.Installed);
        _config.HealthNotificationCooldownSeconds.Should().Be(42);
    }
}
