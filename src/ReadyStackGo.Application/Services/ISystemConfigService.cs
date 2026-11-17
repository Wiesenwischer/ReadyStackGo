namespace ReadyStackGo.Application.Services;

/// <summary>
/// Application layer interface for system configuration persistence.
/// </summary>
public interface ISystemConfigService
{
    /// <summary>
    /// Gets the current wizard state.
    /// </summary>
    Task<WizardState> GetWizardStateAsync();

    /// <summary>
    /// Sets the wizard state.
    /// </summary>
    Task SetWizardStateAsync(WizardState state);

    /// <summary>
    /// Gets the health notification cooldown in seconds.
    /// </summary>
    Task<int> GetHealthNotificationCooldownSecondsAsync();

    /// <summary>
    /// Sets the health notification cooldown in seconds.
    /// </summary>
    Task SetHealthNotificationCooldownSecondsAsync(int seconds);

    /// <summary>
    /// Gets the configured public base URL of the application, used to build links in emails.
    /// </summary>
    Task<string> GetBaseUrlAsync();

    /// <summary>
    /// Gets the base URL only if someone set it (not empty and not the built-in default
    /// <see cref="BaseUrlRules.UnsetDefault"/>), otherwise null.
    /// </summary>
    Task<string?> GetConfiguredBaseUrlAsync();

    /// <summary>
    /// Stores the base URL. The value must be valid per <see cref="BaseUrlRules.Normalize"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the value is not a valid base URL.</exception>
    Task SetBaseUrlAsync(string baseUrl);

    /// <summary>
    /// Gets the default theme id stored for this installation, or null if none is stored.
    /// </summary>
    Task<string?> GetDefaultThemeAsync();

    /// <summary>
    /// Stores the default theme id for this installation unless one is already stored.
    /// Returns true if it was stored.
    /// </summary>
    Task<bool> SetDefaultThemeIfUnsetAsync(string themeId);
}
