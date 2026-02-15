using ReadyStackGo.Application.Services;
using AppWizardState = ReadyStackGo.Application.Services.WizardState;
using InfraWizardState = ReadyStackGo.Infrastructure.Configuration.WizardState;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Infrastructure implementation of ISystemConfigService.
/// Adapts the ConfigStore to the Application layer interface.
/// </summary>
public class SystemConfigService : ISystemConfigService
{
    private readonly IConfigStore _configStore;

    public SystemConfigService(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async Task<AppWizardState> GetWizardStateAsync()
    {
        var config = await _configStore.GetSystemConfigAsync();
        return MapToAppWizardState(config.WizardState);
    }

    public async Task SetWizardStateAsync(AppWizardState state)
    {
        var config = await _configStore.GetSystemConfigAsync();
        config.WizardState = MapToInfraWizardState(state);
        await _configStore.SaveSystemConfigAsync(config);
    }

    private static AppWizardState MapToAppWizardState(InfraWizardState state)
    {
        return state switch
        {
            InfraWizardState.NotStarted => AppWizardState.NotStarted,
            InfraWizardState.Installed => AppWizardState.Installed,
            _ => AppWizardState.NotStarted
        };
    }

    private static InfraWizardState MapToInfraWizardState(AppWizardState state)
    {
        return state switch
        {
            AppWizardState.NotStarted => InfraWizardState.NotStarted,
            AppWizardState.Installed => InfraWizardState.Installed,
            _ => InfraWizardState.NotStarted
        };
    }
}
