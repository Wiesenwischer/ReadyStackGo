using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Application.Services.Impl;

namespace ReadyStackGo.UnitTests.Application.Themes;

public class InstallationThemeInitializerTests
{
    private readonly Mock<ISystemConfigService> _systemConfig = new();

    private InstallationThemeInitializer CreateInitializer() =>
        new(_systemConfig.Object, NullLogger<InstallationThemeInitializer>.Instance);

    [Fact]
    public async Task InstalledWithoutDefault_StoresClassic()
    {
        _systemConfig.Setup(s => s.GetWizardStateAsync()).ReturnsAsync(WizardState.Installed);
        _systemConfig.Setup(s => s.SetDefaultThemeIfUnsetAsync(It.IsAny<string>())).ReturnsAsync(true);

        await CreateInitializer().InitializeOnStartupAsync();

        _systemConfig.Verify(s => s.SetDefaultThemeIfUnsetAsync("classic"), Times.Once);
    }

    [Fact]
    public async Task InstalledWithStoredDefault_DoesNotOverwrite()
    {
        // SetDefaultThemeIfUnsetAsync keeps an existing value; the initializer never sets it unconditionally.
        _systemConfig.Setup(s => s.GetWizardStateAsync()).ReturnsAsync(WizardState.Installed);
        _systemConfig.Setup(s => s.SetDefaultThemeIfUnsetAsync(It.IsAny<string>())).ReturnsAsync(false);

        await CreateInitializer().InitializeOnStartupAsync();

        _systemConfig.Verify(s => s.SetDefaultThemeIfUnsetAsync("classic"), Times.Once);
        _systemConfig.Verify(s => s.GetWizardStateAsync(), Times.Once);
        _systemConfig.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NotInstalled_StoresNothing()
    {
        // A new installation gets its default from the wizard, not from the startup check.
        _systemConfig.Setup(s => s.GetWizardStateAsync()).ReturnsAsync(WizardState.NotStarted);

        await CreateInitializer().InitializeOnStartupAsync();

        _systemConfig.Verify(s => s.SetDefaultThemeIfUnsetAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public void Defaults_AreClassicForExistingAndTurquoiseForNewInstallations()
    {
        ThemeDefaults.ExistingInstallation.Should().Be("classic");
        ThemeDefaults.BuiltIn.Should().Be("turquoise");
    }
}
