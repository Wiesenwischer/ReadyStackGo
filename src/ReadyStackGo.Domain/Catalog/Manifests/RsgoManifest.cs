namespace ReadyStackGo.Domain.Catalog.Manifests;

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

/// <summary>
/// Product metadata for display and organization.
/// A product is the primary deployment unit and can contain one or more stacks.
/// </summary>
public class RsgoProductMetadata
{
    /// <summary>
    /// Human-readable name of the product.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of what the product does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Product version (e.g., "3.1.0").
    /// This is the key differentiator: if set, this manifest is a product.
    /// If not set, this manifest is a fragment (only loadable via include).
    /// </summary>
    public string? ProductVersion { get; set; }

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
    /// Category for organizing products (e.g., "Database", "Web", "Monitoring", "Enterprise").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Tags for filtering and search.
    /// </summary>
    public List<string>? Tags { get; set; }
}

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

/// <summary>
/// Stack metadata for display and organization (used in multi-stack entries and fragments).
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
/// Supports both Docker HEALTHCHECK and RSGO HTTP health checks.
/// </summary>
public class RsgoHealthCheck
{
    #region Docker HEALTHCHECK (for container-level checks)

    /// <summary>
    /// Test command to run (Docker HEALTHCHECK).
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

    #endregion

    #region RSGO HTTP Health Check (for ASP.NET Core /hc endpoints)

    /// <summary>
    /// Health check type: "docker" (default), "http", "tcp", or "none".
    /// - docker: Use Docker HEALTHCHECK (Test command)
    /// - http: RSGO calls HTTP endpoint directly
    /// - tcp: RSGO checks TCP port connectivity
    /// - none: Skip health checks for this service
    /// </summary>
    public string Type { get; set; } = "docker";

    /// <summary>
    /// HTTP path for health endpoint (e.g., "/hc" or "/health").
    /// Only used when Type = "http".
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// Port for HTTP/TCP health checks. Defaults to first exposed port.
    /// Only used when Type = "http" or "tcp".
    /// </summary>
    public int? Port { get; set; }

    /// <summary>
    /// Expected HTTP status codes for healthy response.
    /// Only used when Type = "http". Defaults to [200].
    /// </summary>
    public List<int>? ExpectedStatusCodes { get; set; }

    /// <summary>
    /// Whether to use HTTPS for HTTP health checks.
    /// Only used when Type = "http". Defaults to false.
    /// </summary>
    public bool Https { get; set; } = false;

    #endregion

    /// <summary>
    /// Returns true if this is an RSGO HTTP health check (not Docker HEALTHCHECK).
    /// </summary>
    public bool IsHttpHealthCheck => Type.Equals("http", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if this is an RSGO TCP health check.
    /// </summary>
    public bool IsTcpHealthCheck => Type.Equals("tcp", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns true if health checks are disabled.
    /// </summary>
    public bool IsDisabled => Type.Equals("none", StringComparison.OrdinalIgnoreCase);
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

/// <summary>
/// Maintenance configuration section.
/// Contains observer and future maintenance-related settings.
/// </summary>
public class RsgoMaintenance
{
    /// <summary>
    /// Observer configuration for automatic maintenance mode detection.
    /// </summary>
    public RsgoMaintenanceObserver? Observer { get; set; }
}

/// <summary>
/// Maintenance observer configuration.
/// Monitors external systems to automatically trigger maintenance mode.
/// </summary>
public class RsgoMaintenanceObserver
{
    /// <summary>
    /// Observer type: "sqlExtendedProperty", "sqlQuery", "http", or "file".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Polling interval (e.g., "30s", "1m", "5m").
    /// </summary>
    public string PollingInterval { get; set; } = "30s";

    /// <summary>
    /// Value that indicates maintenance mode is active.
    /// </summary>
    public required string MaintenanceValue { get; set; }

    /// <summary>
    /// Value that indicates normal operation (optional).
    /// If not specified, any value other than maintenanceValue is treated as normal.
    /// </summary>
    public string? NormalValue { get; set; }

    #region SQL Observer Settings (sqlExtendedProperty, sqlQuery)

    /// <summary>
    /// Direct connection string value.
    /// Supports variable substitution: ${VAR_NAME}
    /// </summary>
    public string? ConnectionString { get; set; }

    /// <summary>
    /// Name of a manifest variable containing the connection string.
    /// Alternative to connectionString - use the variable name directly.
    /// </summary>
    public string? ConnectionName { get; set; }

    /// <summary>
    /// For sqlExtendedProperty: Name of the SQL Server extended property to read.
    /// </summary>
    public string? PropertyName { get; set; }

    /// <summary>
    /// For sqlQuery: SQL query that returns a single scalar value.
    /// </summary>
    public string? Query { get; set; }

    #endregion

    #region HTTP Observer Settings

    /// <summary>
    /// For http: URL to call for maintenance state check.
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// For http: HTTP method (GET, POST, etc.). Defaults to GET.
    /// </summary>
    public string? Method { get; set; }

    /// <summary>
    /// For http: Optional headers to include in the request.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// For http: Request timeout (e.g., "10s"). Defaults to 10s.
    /// </summary>
    public string? Timeout { get; set; }

    /// <summary>
    /// For http: JSONPath expression to extract the value from response.
    /// If not specified, the entire response body is used.
    /// </summary>
    public string? JsonPath { get; set; }

    #endregion

    #region File Observer Settings

    /// <summary>
    /// For file: Path to the file to monitor.
    /// </summary>
    public string? Path { get; set; }

    /// <summary>
    /// For file: Check mode - "exists" or "content".
    /// - exists: Returns "true" if file exists, "false" otherwise
    /// - content: Reads file content (optionally with pattern extraction)
    /// </summary>
    public string? Mode { get; set; }

    /// <summary>
    /// For file (content mode): Regex pattern to extract value from file content.
    /// First capture group is used, or entire match if no groups.
    /// </summary>
    public string? ContentPattern { get; set; }

    #endregion
}
