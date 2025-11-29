namespace ReadyStackGo.Domain.Stacks;

/// <summary>
/// A stack definition loaded from a stack source
/// </summary>
public class StackDefinition
{
    /// <summary>
    /// Unique identifier (format: sourceId:stackName)
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// ID of the source this stack came from
    /// </summary>
    public required string SourceId { get; init; }

    /// <summary>
    /// Name of the stack
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Optional description
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// The Docker Compose YAML content
    /// </summary>
    public required string YamlContent { get; init; }

    /// <summary>
    /// Environment variables found in the YAML
    /// </summary>
    public List<StackVariable> Variables { get; init; } = new();

    /// <summary>
    /// Services defined in the stack
    /// </summary>
    public List<string> Services { get; init; } = new();

    /// <summary>
    /// Path to the source file (for display purposes)
    /// </summary>
    public string? FilePath { get; init; }

    /// <summary>
    /// Relative path from the stack source root (e.g., "examples" or "ams.project")
    /// Used for display in the UI to show where the stack is located
    /// </summary>
    public string? RelativePath { get; init; }

    /// <summary>
    /// Additional compose files (e.g., docker-compose.override.yml)
    /// These will be merged with the main YamlContent during deployment
    /// </summary>
    public List<string> AdditionalFiles { get; init; } = new();

    /// <summary>
    /// Content of additional compose files, keyed by filename
    /// </summary>
    public Dictionary<string, string> AdditionalFileContents { get; init; } = new();

    /// <summary>
    /// When this definition was last synced from the source
    /// </summary>
    public DateTime LastSyncedAt { get; init; }

    /// <summary>
    /// Version or hash of the stack (for change detection)
    /// </summary>
    public string? Version { get; init; }
}

/// <summary>
/// An environment variable in a stack
/// </summary>
public record StackVariable
{
    /// <summary>
    /// Variable name
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Default value if specified
    /// </summary>
    public string? DefaultValue { get; init; }

    /// <summary>
    /// Whether this variable is required (no default value)
    /// </summary>
    public bool IsRequired { get; init; }

    /// <summary>
    /// Optional description from comments
    /// </summary>
    public string? Description { get; init; }
}
