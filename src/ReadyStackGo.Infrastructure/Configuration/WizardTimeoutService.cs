using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// Service for managing wizard timeout window.
///
/// Behavior:
/// - Timer starts at application startup (not first browser access)
/// - If no admin is created within the timeout window, wizard is permanently locked
/// - Lock can only be reset by restarting the container
/// - This prevents attackers from taking over an unattended fresh installation
/// </summary>
public class WizardTimeoutService : IWizardTimeoutService
{
    private readonly IConfigStore _configStore;
    private readonly ILogger<WizardTimeoutService> _logger;
    private readonly int _timeoutSeconds;

    // In-memory flag to track if we've already initialized on this startup
    private static bool _startupInitialized;
    private static readonly object _initLock = new();

    /// <summary>
    /// Reset the startup initialization flag. Only for testing purposes.
    /// </summary>
    internal static void ResetStartupFlag()
    {
        lock (_initLock)
        {
            _startupInitialized = false;
        }
    }

    public WizardTimeoutService(
        IConfigStore configStore,
        IConfiguration configuration,
        ILogger<WizardTimeoutService> logger)
    {
        _configStore = configStore;
        _logger = logger;
        _timeoutSeconds = configuration.GetValue("Wizard:TimeoutSeconds", 300); // Default 5 minutes
    }

    /// <summary>
    /// Initialize the timeout window at application startup.
    /// This should be called once during application bootstrap.
    /// </summary>
    public async Task InitializeOnStartupAsync()
    {
        lock (_initLock)
        {
            if (_startupInitialized)
                return;
            _startupInitialized = true;
        }

        var config = await _configStore.GetSystemConfigAsync();

        // If wizard is already completed, nothing to do
        if (config.WizardState == WizardState.Installed)
        {
            _logger.LogDebug("Wizard already completed, skipping timeout initialization");
            return;
        }

        // Clear any previous lock from last container run (container restart = fresh chance)
        // and start fresh timeout window
        if (config.IsWizardLocked)
        {
            _logger.LogInformation("Clearing previous wizard lock due to container restart.");
        }
        config.IsWizardLocked = false;
        config.WizardStartedAt = DateTime.UtcNow;
        config.WizardState = WizardState.NotStarted;
        await _configStore.SaveSystemConfigAsync(config);

        _logger.LogInformation(
            "Wizard timeout window started at {StartedAt}. Admin must be created within {Seconds} seconds.",
            config.WizardStartedAt,
            _timeoutSeconds);
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
                IsLocked = false,
                StartedAt = null,
                ExpiresAt = null,
                RemainingSeconds = null,
                TimeoutSeconds = _timeoutSeconds
            };
        }

        // If locked, return locked state
        if (config.IsWizardLocked)
        {
            return new WizardTimeoutInfo
            {
                IsTimedOut = true,
                IsLocked = true,
                StartedAt = config.WizardStartedAt,
                ExpiresAt = config.WizardStartedAt?.AddSeconds(_timeoutSeconds),
                RemainingSeconds = null,
                TimeoutSeconds = _timeoutSeconds
            };
        }

        // If not yet started (shouldn't happen after InitializeOnStartupAsync), initialize now
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

        // If timed out, set the permanent lock
        if (isTimedOut && !config.IsWizardLocked)
        {
            config.IsWizardLocked = true;
            config.WizardState = WizardState.NotStarted; // Reset any partial progress
            await _configStore.SaveSystemConfigAsync(config);
            _logger.LogWarning(
                "Wizard timeout expired! Setup window locked. Restart container to try again.");
        }

        return new WizardTimeoutInfo
        {
            IsTimedOut = isTimedOut,
            IsLocked = config.IsWizardLocked,
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

    public async Task<bool> IsLockedAsync()
    {
        var config = await _configStore.GetSystemConfigAsync();
        return config.IsWizardLocked;
    }

    public async Task ResetTimeoutAsync()
    {
        // Reset via API is NOT allowed - lock can only be cleared by container restart
        // Lock can only be cleared by container restart (which calls InitializeOnStartupAsync)
        var config = await _configStore.GetSystemConfigAsync();

        if (config.IsWizardLocked)
        {
            _logger.LogWarning("Cannot reset wizard timeout - wizard is locked. Restart the container to reset.");
            return;
        }

        // Only reset partial wizard state, not the timeout itself
        if (config.WizardState != WizardState.Installed)
        {
            _logger.LogInformation("Resetting partial wizard state due to timeout. State was: {State}",
                config.WizardState);
            config.WizardState = WizardState.NotStarted;
            await _configStore.SaveSystemConfigAsync(config);
        }
    }

    public async Task ClearTimeoutAsync()
    {
        var config = await _configStore.GetSystemConfigAsync();

        if (config.WizardStartedAt != null || config.IsWizardLocked)
        {
            _logger.LogInformation("Clearing wizard timeout tracking. Wizard completed successfully.");
            config.WizardStartedAt = null;
            config.IsWizardLocked = false;
            await _configStore.SaveSystemConfigAsync(config);
        }
    }
}
