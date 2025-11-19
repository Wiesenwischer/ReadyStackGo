namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// System configuration stored in rsgo.system.json
/// </summary>
public class SystemConfig
{
    public Organization? Organization { get; set; }
    public string BaseUrl { get; set; } = "http://localhost:5000";
    public int HttpPort { get; set; } = 5000;
    public int HttpsPort { get; set; } = 5001;
    public string DockerNetwork { get; set; } = "rsgo-net";
    public DeploymentMode Mode { get; set; } = DeploymentMode.SingleNode;
    public WizardState WizardState { get; set; } = WizardState.NotStarted;
}
