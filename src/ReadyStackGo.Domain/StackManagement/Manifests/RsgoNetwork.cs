namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Network definition.
/// </summary>
public class RsgoNetwork
{
    /// <summary>
    /// Network driver.
    /// </summary>
    public string? Driver { get; set; }

    /// <summary>
    /// Whether this is an external network.
    /// </summary>
    public bool? External { get; set; }

    /// <summary>
    /// Driver options.
    /// </summary>
    public Dictionary<string, string>? DriverOpts { get; set; }
}
