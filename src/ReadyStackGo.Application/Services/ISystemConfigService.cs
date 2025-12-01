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
}
