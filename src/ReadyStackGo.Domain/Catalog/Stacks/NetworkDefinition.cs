namespace ReadyStackGo.Domain.Catalog.Stacks;

/// <summary>
/// Defines a network for a stack.
/// This is a top-level network definition (the "networks:" section in a compose file).
/// </summary>
public record NetworkDefinition
{
    /// <summary>
    /// Name of the network.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Network driver to use (e.g., "bridge", "overlay", "host").
    /// </summary>
    public string Driver { get; init; } = "bridge";

    /// <summary>
    /// Driver-specific options.
    /// </summary>
    public IReadOnlyDictionary<string, string> DriverOpts { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Labels to apply to the network.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Whether this is an external network (not managed by the stack).
    /// </summary>
    public bool External { get; init; }

    /// <summary>
    /// External network name (if different from the local name).
    /// </summary>
    public string? ExternalName { get; init; }

    /// <summary>
    /// Whether this network is attachable (for overlay networks).
    /// </summary>
    public bool Attachable { get; init; }

    /// <summary>
    /// Whether this is an internal network (no external connectivity).
    /// </summary>
    public bool Internal { get; init; }

    /// <summary>
    /// IPAM configuration.
    /// </summary>
    public NetworkIpam? Ipam { get; init; }
}

/// <summary>
/// IP Address Management configuration for a network.
/// </summary>
public record NetworkIpam
{
    /// <summary>
    /// IPAM driver.
    /// </summary>
    public string Driver { get; init; } = "default";

    /// <summary>
    /// Subnet configurations.
    /// </summary>
    public IReadOnlyList<IpamConfig> Config { get; init; } = Array.Empty<IpamConfig>();
}

/// <summary>
/// IPAM subnet configuration.
/// </summary>
public record IpamConfig
{
    /// <summary>
    /// Subnet in CIDR format (e.g., "172.28.0.0/16").
    /// </summary>
    public string? Subnet { get; init; }

    /// <summary>
    /// Gateway IP address.
    /// </summary>
    public string? Gateway { get; init; }

    /// <summary>
    /// IP range for allocation.
    /// </summary>
    public string? IpRange { get; init; }
}
