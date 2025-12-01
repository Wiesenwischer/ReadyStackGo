namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for managing wizard timeout window.
/// The wizard has a 5-minute window after first access for admin creation.
/// After timeout, the wizard resets and a new window starts.
/// </summary>
public interface IWizardTimeoutService
{
    /// <summary>
    /// Gets the timeout information for the current wizard session.
    /// Initializes the timeout window on first call if wizard is not completed.
    /// </summary>
    Task<WizardTimeoutInfo> GetTimeoutInfoAsync();

    /// <summary>
    /// Checks if the wizard window has timed out.
    /// </summary>
    Task<bool> IsTimedOutAsync();

    /// <summary>
    /// Resets the wizard timeout window. Called when wizard times out
    /// or when explicitly resetting the wizard.
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
