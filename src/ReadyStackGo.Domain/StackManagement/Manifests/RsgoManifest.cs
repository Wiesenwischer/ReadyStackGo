namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// RSGo Manifest format - the native stack definition format for ReadyStackGo.
/// v0.10: Specification for Custom Manifest Format
///
/// This format extends Docker Compose with:
/// - Type validation for variables
/// - Regex validation for input fields
/// - Select options for dropdown inputs
/// - Rich metadata (description, labels, documentation)
/// </summary>
public class RsgoManifest
{
    /// <summary>
    /// Manifest format version (e.g., "1.0").
    /// </summary>
    public string Version { get; set; } = "1.0";

    /// <summary>
    /// Stack metadata.
    /// </summary>
    public RsgoStackMetadata Metadata { get; set; } = new();

    /// <summary>
    /// Variable definitions with type information and validation.
    /// </summary>
    public Dictionary<string, RsgoVariable> Variables { get; set; } = new();

    /// <summary>
    /// Service definitions (similar to Docker Compose services).
    /// </summary>
    public Dictionary<string, RsgoService> Services { get; set; } = new();

    /// <summary>
    /// Volume definitions.
    /// </summary>
    public Dictionary<string, RsgoVolume>? Volumes { get; set; }

    /// <summary>
    /// Network definitions.
    /// </summary>
    public Dictionary<string, RsgoNetwork>? Networks { get; set; }
}

/// <summary>
/// Stack metadata for display and organization.
/// </summary>
public class RsgoStackMetadata
{
    /// <summary>
    /// Human-readable name of the stack.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of what the stack does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Stack version (for user reference, not deployment).
    /// </summary>
    public string? StackVersion { get; set; }

    /// <summary>
    /// Author or maintainer.
    /// </summary>
    public string? Author { get; set; }

    /// <summary>
    /// URL to documentation or project homepage.
    /// </summary>
    public string? Documentation { get; set; }

    /// <summary>
    /// Icon URL for UI display.
    /// </summary>
    public string? Icon { get; set; }

    /// <summary>
    /// Category for organizing stacks (e.g., "Database", "Web", "Monitoring").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Tags for filtering and search.
    /// </summary>
    public List<string>? Tags { get; set; }
}

/// <summary>
/// Variable definition with type, validation, and UI hints.
/// </summary>
public class RsgoVariable
{
    /// <summary>
    /// Human-readable label for the variable.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Description shown as help text in UI.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Variable type for validation and UI rendering.
    /// </summary>
    public VariableType Type { get; set; } = VariableType.String;

    /// <summary>
    /// Default value.
    /// </summary>
    public string? Default { get; set; }

    /// <summary>
    /// Whether the variable is required.
    /// </summary>
    public bool Required { get; set; } = false;

    /// <summary>
    /// Regex pattern for validation (only for String type).
    /// </summary>
    public string? Pattern { get; set; }

    /// <summary>
    /// Error message when pattern validation fails.
    /// </summary>
    public string? PatternError { get; set; }

    /// <summary>
    /// Options for Select type variables.
    /// </summary>
    public List<RsgoSelectOption>? Options { get; set; }

    /// <summary>
    /// Minimum value (for Number type).
    /// </summary>
    public double? Min { get; set; }

    /// <summary>
    /// Maximum value (for Number type).
    /// </summary>
    public double? Max { get; set; }

    /// <summary>
    /// Placeholder text for input field.
    /// </summary>
    public string? Placeholder { get; set; }

    /// <summary>
    /// Group name for organizing variables in UI.
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// Display order within group.
    /// </summary>
    public int Order { get; set; } = 0;
}

/// <summary>
/// Option for Select type variables.
/// </summary>
public class RsgoSelectOption
{
    /// <summary>
    /// Value to use when this option is selected.
    /// </summary>
    public required string Value { get; set; }

    /// <summary>
    /// Label to display in the UI.
    /// </summary>
    public string? Label { get; set; }

    /// <summary>
    /// Description or help text for this option.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Service definition (Docker container).
/// </summary>
public class RsgoService
{
    /// <summary>
    /// Docker image to use.
    /// </summary>
    public required string Image { get; set; }

    /// <summary>
    /// Container name (defaults to stack_servicename).
    /// </summary>
    public string? ContainerName { get; set; }

    /// <summary>
    /// Environment variables for this service.
    /// Values can reference variables using ${VAR_NAME} syntax.
    /// </summary>
    public Dictionary<string, string>? Environment { get; set; }

    /// <summary>
    /// Port mappings (host:container format).
    /// </summary>
    public List<string>? Ports { get; set; }

    /// <summary>
    /// Volume mappings.
    /// </summary>
    public List<string>? Volumes { get; set; }

    /// <summary>
    /// Networks to connect to.
    /// </summary>
    public List<string>? Networks { get; set; }

    /// <summary>
    /// Service dependencies (other service names).
    /// </summary>
    public List<string>? DependsOn { get; set; }

    /// <summary>
    /// Restart policy.
    /// </summary>
    public string? Restart { get; set; }

    /// <summary>
    /// Container command override.
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Container entrypoint override.
    /// </summary>
    public string? Entrypoint { get; set; }

    /// <summary>
    /// Working directory in the container.
    /// </summary>
    public string? WorkingDir { get; set; }

    /// <summary>
    /// User to run as.
    /// </summary>
    public string? User { get; set; }

    /// <summary>
    /// Container labels.
    /// </summary>
    public Dictionary<string, string>? Labels { get; set; }

    /// <summary>
    /// Health check configuration.
    /// </summary>
    public RsgoHealthCheck? HealthCheck { get; set; }
}

/// <summary>
/// Health check configuration.
/// </summary>
public class RsgoHealthCheck
{
    /// <summary>
    /// Test command to run.
    /// </summary>
    public List<string>? Test { get; set; }

    /// <summary>
    /// Interval between checks (e.g., "30s").
    /// </summary>
    public string? Interval { get; set; }

    /// <summary>
    /// Timeout for each check (e.g., "10s").
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>
    /// Number of retries before marking unhealthy.
    /// </summary>
    public int? Retries { get; set; }

    /// <summary>
    /// Start period before checks begin (e.g., "5s").
    /// </summary>
    public string? StartPeriod { get; set; }
}

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
