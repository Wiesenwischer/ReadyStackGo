using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Infrastructure implementation of IOnboardingStateService.
/// Persists the onboarding dismissed state via the system config file.
/// </summary>
public class OnboardingStateService : IOnboardingStateService
{
    private readonly IConfigStore _configStore;

    public OnboardingStateService(IConfigStore configStore)
    {
        _configStore = configStore;
    }

    public async Task<bool> IsDismissedAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetSystemConfigAsync();
        return config.OnboardingDismissed;
    }

    public async Task DismissAsync(CancellationToken cancellationToken = default)
    {
        var config = await _configStore.GetSystemConfigAsync();
        config.OnboardingDismissed = true;
        await _configStore.SaveSystemConfigAsync(config);
    }
}
