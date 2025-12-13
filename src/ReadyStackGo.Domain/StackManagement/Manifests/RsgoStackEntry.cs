namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Stack entry in a multi-stack product.
/// Can be either an inline definition or an include reference.
/// </summary>
public class RsgoStackEntry
{
    /// <summary>
    /// Path to external file to include (relative to the manifest file).
    /// If set, this is an include reference. Other properties are ignored.
    /// </summary>
    public string? Include { get; set; }

    /// <summary>
    /// Stack metadata (for inline definitions).
    /// </summary>
    public RsgoStackMetadata? Metadata { get; set; }

    /// <summary>
    /// Variable definitions (for inline definitions).
    /// </summary>
    public Dictionary<string, RsgoVariable>? Variables { get; set; }

    /// <summary>
    /// Service definitions (for inline definitions).
    /// </summary>
    public Dictionary<string, RsgoService>? Services { get; set; }

    /// <summary>
    /// Volume definitions (for inline definitions).
    /// </summary>
    public Dictionary<string, RsgoVolume>? Volumes { get; set; }

    /// <summary>
    /// Network definitions (for inline definitions).
    /// </summary>
    public Dictionary<string, RsgoNetwork>? Networks { get; set; }

    /// <summary>
    /// Determines if this is an include reference.
    /// </summary>
    public bool IsInclude => !string.IsNullOrEmpty(Include);
}
