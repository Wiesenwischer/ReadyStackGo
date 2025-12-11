namespace ReadyStackGo.Domain.Catalog.Stacks;

/// <summary>
/// Defines a named volume for a stack.
/// This is a top-level volume definition (the "volumes:" section in a compose file).
/// </summary>
public record VolumeDefinition
{
    /// <summary>
    /// Name of the volume.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Volume driver to use (e.g., "local", "nfs").
    /// </summary>
    public string? Driver { get; init; }

    /// <summary>
    /// Driver-specific options.
    /// </summary>
    public IReadOnlyDictionary<string, string> DriverOpts { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Labels to apply to the volume.
    /// </summary>
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();

    /// <summary>
    /// Whether this is an external volume (not managed by the stack).
    /// </summary>
    public bool External { get; init; }

    /// <summary>
    /// External volume name (if different from the local name).
    /// </summary>
    public string? ExternalName { get; init; }
}
