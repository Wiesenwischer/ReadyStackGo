namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for managing wizard timeout window.
///
/// Behavior:
/// - Timer starts at application startup (not first browser access)
/// - If no admin is created within the timeout window, wizard is permanently locked
/// - Lock can only be reset by restarting the container
/// - This prevents attackers from taking over an unattended fresh installation
/// </summary>
public interface IWizardTimeoutService
{
    /// <summary>
    /// Initialize the timeout window at application startup.
    /// This should be called once during application bootstrap.
    /// </summary>
    Task InitializeOnStartupAsync();

    /// <summary>
    /// Gets the timeout information for the current wizard session.
    /// </summary>
    Task<WizardTimeoutInfo> GetTimeoutInfoAsync();

    /// <summary>
    /// Checks if the wizard window has timed out.
    /// </summary>
    Task<bool> IsTimedOutAsync();

    /// <summary>
    /// Checks if the wizard is permanently locked (timeout expired, requires container restart).
    /// </summary>
    Task<bool> IsLockedAsync();

    /// <summary>
    /// Resets partial wizard state. This does NOT reset the timeout lock -
    /// that requires a container restart.
    /// </summary>
    Task ResetTimeoutAsync();

    /// <summary>
    /// Clears the timeout tracking. Called when wizard is completed successfully.
    /// </summary>
    Task ClearTimeoutAsync();
}

/// <summary>
/// Information about the wizard timeout window.
/// </summary>
public class WizardTimeoutInfo
{
    /// <summary>
    /// Whether the wizard window has timed out.
    /// </summary>
    public bool IsTimedOut { get; init; }

    /// <summary>
    /// Whether the wizard is permanently locked (requires container restart to reset).
    /// </summary>
    public bool IsLocked { get; init; }

    /// <summary>
    /// When the wizard window started.
    /// </summary>
    public DateTime? StartedAt { get; init; }

    /// <summary>
    /// When the wizard window will expire.
    /// </summary>
    public DateTime? ExpiresAt { get; init; }

    /// <summary>
    /// Remaining seconds until timeout. Null if already timed out or not started.
    /// </summary>
    public int? RemainingSeconds { get; init; }

    /// <summary>
    /// The configured timeout duration in seconds.
    /// </summary>
    public int TimeoutSeconds { get; init; }
}
