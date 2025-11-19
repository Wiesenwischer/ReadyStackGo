namespace ReadyStackGo.Domain.Configuration;

/// <summary>
/// Release status configuration stored in rsgo.release.json
/// </summary>
public class ReleaseConfig
{
    public string? InstalledStackVersion { get; set; }
    public Dictionary<string, string> InstalledContexts { get; set; } = new();
    public DateTime? InstallDate { get; set; }
}
