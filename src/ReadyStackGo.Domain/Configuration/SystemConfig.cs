using ReadyStackGo.Domain.Organizations;

namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// System configuration stored in rsgo.system.json.
/// Contains the organization aggregate with its environments.
/// </summary>
public class SystemConfig
{
    /// <summary>
    /// The organization with its environments.
    /// Null until wizard step 2 is completed.
    /// </summary>
    public Organization? Organization { get; set; }

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
}
