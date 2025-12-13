namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Volume definition.
/// </summary>
public class RsgoVolume
{
    /// <summary>
    /// Volume driver.
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Whether this is an external volume.
    /// </summary>
    public bool? External { get; set; }

    /// <summary>
    /// Driver options.
    /// </summary>
    public Dictionary<string, string>? DriverOpts { get; set; }
}
