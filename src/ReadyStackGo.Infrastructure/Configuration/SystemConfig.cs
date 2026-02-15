namespace ReadyStackGo.Infrastructure.Configuration;

/// <summary>
/// System configuration stored in rsgo.system.json.
/// v0.6: Organization and Environments are now stored in SQLite, not in this config file.
/// </summary>
public class SystemConfig
{
    /// <summary>
    /// Base URL for the application
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:5000";

    /// <summary>
    /// HTTP port
    /// </summary>
    public int HttpPort { get; set; } = 5000;

    /// <summary>
    /// HTTPS port
    /// </summary>
    public int HttpsPort { get; set; } = 5001;

    /// <summary>
    /// Docker network name for container communication
    /// </summary>
    public string DockerNetwork { get; set; } = "rsgo-net";

    /// <summary>
    /// Deployment mode (SingleNode in v0.4, MultiNode deferred to v2.0+)
    /// </summary>
    public DeploymentMode Mode { get; set; } = DeploymentMode.SingleNode;

    /// <summary>
    /// Current wizard state
    /// </summary>
    public WizardState WizardState { get; set; } = WizardState.NotStarted;

    /// <summary>
    /// Installed version of the application
    /// </summary>
    public string? InstalledVersion { get; set; }

    /// <summary>
    /// Timestamp when the wizard window was first opened (server start).
    /// Used for wizard timeout enforcement (5-minute window).
    /// </summary>
    public DateTime? WizardStartedAt { get; set; }

    /// <summary>
    /// Whether the wizard has been permanently locked due to timeout.
    /// Once locked, can only be reset by restarting the container (clears in-memory state).
    /// </summary>
    public bool IsWizardLocked { get; set; }

    /// <summary>
    /// Whether the user has dismissed the onboarding checklist on the dashboard.
    /// </summary>
    public bool OnboardingDismissed { get; set; }
}
