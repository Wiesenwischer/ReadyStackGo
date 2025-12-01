using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;
using AppWizardState = ReadyStackGo.Application.Services.WizardState;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Service for managing wizard timeout window.
/// The wizard has a configurable window (default 5 minutes) after first access for admin creation.
/// After timeout, any partial wizard state is reset and a new window starts on next access.
/// </summary>
public class WizardTimeoutService : IWizardTimeoutService
{
    private readonly IConfigStore _configStore;
    private readonly ILogger<WizardTimeoutService> _logger;
    private readonly int _timeoutSeconds;

    public WizardTimeoutService(
        IConfigStore configStore,
        IConfiguration configuration,
        ILogger<WizardTimeoutService> logger)
    {
        _configStore = configStore;
        _logger = logger;
        _timeoutSeconds = configuration.GetValue("Wizard:TimeoutSeconds", 300); // Default 5 minutes
    }

    public async Task<WizardTimeoutInfo> GetTimeoutInfoAsync()
    {
        var config = await _configStore.GetSystemConfigAsync();

        // If wizard is completed, no timeout applies
        if (config.WizardState == WizardState.Installed)
        {
            return new WizardTimeoutInfo
            {
                IsTimedOut = false,
                StartedAt = null,
                ExpiresAt = null,
                RemainingSeconds = null,
                TimeoutSeconds = _timeoutSeconds
            };
        }

        // Initialize timeout window if not started
        if (config.WizardStartedAt == null)
        {
            config.WizardStartedAt = DateTime.UtcNow;
            await _configStore.SaveSystemConfigAsync(config);
            _logger.LogInformation("Wizard timeout window started at {StartedAt}, expires in {Seconds} seconds",
                config.WizardStartedAt, _timeoutSeconds);
        }

        var startedAt = config.WizardStartedAt.Value;
        var expiresAt = startedAt.AddSeconds(_timeoutSeconds);
        var now = DateTime.UtcNow;
        var isTimedOut = now >= expiresAt;
        var remainingSeconds = isTimedOut ? 0 : (int)(expiresAt - now).TotalSeconds;

        return new WizardTimeoutInfo
        {
            IsTimedOut = isTimedOut,
            StartedAt = startedAt,
            ExpiresAt = expiresAt,
            RemainingSeconds = isTimedOut ? null : remainingSeconds,
            TimeoutSeconds = _timeoutSeconds
        };
    }

    public async Task<bool> IsTimedOutAsync()
    {
        var info = await GetTimeoutInfoAsync();
        return info.IsTimedOut;
    }

    public async Task ResetTimeoutAsync()
    {
        var config = await _configStore.GetSystemConfigAsync();

        // Only reset if wizard is not completed
        if (config.WizardState == WizardState.Installed)
        {
            _logger.LogDebug("Wizard is already completed, not resetting timeout");
            return;
        }

        _logger.LogInformation("Resetting wizard timeout window. Previous state: {State}, StartedAt: {StartedAt}",
            config.WizardState, config.WizardStartedAt);

        // Reset wizard state to NotStarted and clear timeout
        config.WizardState = WizardState.NotStarted;
        config.WizardStartedAt = null;
        await _configStore.SaveSystemConfigAsync(config);
    }

    public async Task ClearTimeoutAsync()
    {
        var config = await _configStore.GetSystemConfigAsync();

        if (config.WizardStartedAt != null)
        {
            _logger.LogInformation("Clearing wizard timeout tracking. Wizard completed successfully.");
            config.WizardStartedAt = null;
            await _configStore.SaveSystemConfigAsync(config);
        }
    }
}
