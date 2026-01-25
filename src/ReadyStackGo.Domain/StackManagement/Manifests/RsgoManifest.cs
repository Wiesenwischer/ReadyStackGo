namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// RSGo Manifest format - the native stack definition format for ReadyStackGo.
///
/// This format extends Docker Compose with:
/// - Type validation for variables (String, Number, Boolean, Select, Password, Port)
/// - Regex validation for input fields
/// - Select options for dropdown inputs
/// - Rich metadata (description, labels, documentation)
/// - Multi-stack products with shared variables
/// - Include support for modular stack definitions
///
/// Two types of manifests:
/// 1. Product Manifest: Has metadata.productVersion, contains one or more stacks
/// 2. Stack Fragment: No productVersion, only loadable via include
/// </summary>
public class RsgoManifest
{
    /// <summary>
    /// Reserved for future use. Currently ignored by the parser.
    /// Format is auto-detected based on structure (metadata, services, stacks).
    /// </summary>
    public string? Version { get; set; }

    /// <summary>
    /// Product/Stack metadata. If ProductVersion is set, this is a product manifest.
    /// </summary>
    public RsgoProductMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Shared variables applied to all stacks in this product.
    /// These are merged with stack-specific variables (stack variables take precedence).
    /// </summary>
    public Dictionary<string, RsgoVariable>? SharedVariables { get; set; }

    /// <summary>
    /// Stack definitions. Each entry can be:
    /// - Inline: Full stack definition with services, variables, etc.
    /// - Include: Reference to external file via "include" property
    /// </summary>
    public Dictionary<string, RsgoStackEntry>? Stacks { get; set; }

    /// <summary>
    /// Variable definitions (for single-stack manifests or fragments).
    /// </summary>
    public Dictionary<string, RsgoVariable>? Variables { get; set; }

    /// <summary>
    /// List of service include files. Services from these files are merged into the Services dictionary.
    /// This allows splitting large service definitions across multiple files for better organization.
    /// Example: ["Contexts/projectmanagement.yaml", "Contexts/memo.yaml"]
    /// </summary>
    public List<string>? ServiceIncludes { get; set; }

    /// <summary>
    /// Service definitions (for single-stack manifests or fragments).
    /// </summary>
    public Dictionary<string, RsgoService>? Services { get; set; }

    /// <summary>
    /// Volume definitions.
    /// </summary>
    public Dictionary<string, RsgoVolume>? Volumes { get; set; }

    /// <summary>
    /// Network definitions.
    /// </summary>
    public Dictionary<string, RsgoNetwork>? Networks { get; set; }

    /// <summary>
    /// Maintenance configuration section (optional).
    /// Contains observer for automatic maintenance mode detection.
    /// </summary>
    public RsgoMaintenance? Maintenance { get; set; }

    /// <summary>
    /// Determines if this manifest is a product (has productVersion).
    /// Products can contain multiple stacks and are the primary deployment unit.
    /// Manifests without productVersion are fragments, only loadable via include.
    /// </summary>
    public bool IsProduct => !string.IsNullOrEmpty(Metadata?.ProductVersion);

    /// <summary>
    /// Determines if this is a single-stack manifest (no stacks section, has services directly).
    /// </summary>
    public bool IsSingleStack => Stacks == null && Services != null && Services.Count > 0;

    /// <summary>
    /// Determines if this is a multi-stack manifest (has stacks section).
    /// </summary>
    public bool IsMultiStack => Stacks != null && Stacks.Count > 0;
}
