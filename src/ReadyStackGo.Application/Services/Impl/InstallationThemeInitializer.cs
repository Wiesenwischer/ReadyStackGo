using Microsoft.Extensions.Logging;

namespace ReadyStackGo.Application.Services.Impl;

/// <summary>
/// Keeps the look of installations that were set up before the theme packages existed:
/// on startup, an installed system without a stored default theme gets <see cref="ThemeDefaults.ExistingInstallation"/>.
/// New installations store <see cref="ThemeDefaults.BuiltIn"/> when the wizard completes, so this runs only once
/// per updated installation.
/// </summary>
public class InstallationThemeInitializer
{
    private readonly ISystemConfigService _systemConfigService;
    private readonly ILogger<InstallationThemeInitializer> _logger;

    public InstallationThemeInitializer(
        ISystemConfigService systemConfigService,
        ILogger<InstallationThemeInitializer> logger)
    {
        _systemConfigService = systemConfigService;
        _logger = logger;
    }

    public async Task InitializeOnStartupAsync()
    {
        if (await _systemConfigService.GetWizardStateAsync() != WizardState.Installed)
        {
            // Not set up yet: the wizard stores the default for a new installation.
            return;
        }

        if (await _systemConfigService.SetDefaultThemeIfUnsetAsync(ThemeDefaults.ExistingInstallation))
        {
            _logger.LogInformation(
                "Existing installation without a default theme: keeping the previous look ({Theme})",
                ThemeDefaults.ExistingInstallation);
        }
    }
}
