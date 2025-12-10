namespace ReadyStackGo.Domain.Catalog.Stacks;

using ReadyStackGo.Domain.Catalog.Manifests;
using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// A stack definition loaded from a stack source.
/// This is a Value Object as it has no identity of its own - it's identified by SourceId:Name.
/// </summary>
public class StackDefinition
{
    /// <summary>
    /// Unique identifier (format: sourceId:stackName).
    /// </summary>
    public string Id => $"{SourceId}:{Name}";

    /// <summary>
    /// ID of the source this stack came from.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Name of the stack.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Optional description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// The Docker Compose YAML content.
    /// </summary>
    public string YamlContent { get; }

    /// <summary>
    /// Environment variables found in the YAML.
    /// </summary>
    public IReadOnlyList<StackVariable> Variables { get; }

    /// <summary>
    /// Services defined in the stack.
    /// </summary>
    public IReadOnlyList<string> Services { get; }

    /// <summary>
    /// Path to the source file (for display purposes).
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Relative path from the stack source root.
    /// </summary>
    public string? RelativePath { get; }

    /// <summary>
    /// Additional compose files (e.g., docker-compose.override.yml).
    /// </summary>
    public IReadOnlyList<string> AdditionalFiles { get; }

    /// <summary>
    /// Content of additional compose files, keyed by filename.
    /// </summary>
    public IReadOnlyDictionary<string, string> AdditionalFileContents { get; }

    /// <summary>
    /// When this definition was last synced from the source.
    /// </summary>
    public DateTime LastSyncedAt { get; }

    /// <summary>
    /// Version or hash of the stack (for change detection).
    /// </summary>
    public string? Version { get; }

    #region Product Properties (for grouping stacks into products)

    /// <summary>
    /// Name of the parent product this stack belongs to.
    /// For single-stack products, this equals the stack name.
    /// For multi-stack products, all stacks share the same ProductName.
    /// </summary>
    public string ProductName { get; }

    /// <summary>
    /// Human-readable display name of the product.
    /// </summary>
    public string ProductDisplayName { get; }

    /// <summary>
    /// Description of the product (may differ from stack description).
    /// </summary>
    public string? ProductDescription { get; }

    /// <summary>
    /// Product version (from metadata.productVersion).
    /// </summary>
    public string? ProductVersion { get; }

    /// <summary>
    /// Category for organizing products (e.g., "Database", "Web", "Identity").
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Tags for filtering and search.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    #endregion

    #region Maintenance Configuration

    /// <summary>
    /// Maintenance observer configuration for this stack.
    /// Defined at product-level in the manifest and inherited by all stacks.
    /// </summary>
    public RsgoMaintenanceObserver? MaintenanceObserver { get; }

    #endregion

    public StackDefinition(
        string sourceId,
        string name,
        string yamlContent,
        string? description = null,
        IEnumerable<StackVariable>? variables = null,
        IEnumerable<string>? services = null,
        string? filePath = null,
        string? relativePath = null,
        IEnumerable<string>? additionalFiles = null,
        IDictionary<string, string>? additionalFileContents = null,
        DateTime? lastSyncedAt = null,
        string? version = null,
        // Product properties
        string? productName = null,
        string? productDisplayName = null,
        string? productDescription = null,
        string? productVersion = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        // Maintenance configuration
        RsgoMaintenanceObserver? maintenanceObserver = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));
        if (string.IsNullOrWhiteSpace(yamlContent))
            throw new ArgumentException("YamlContent cannot be empty.", nameof(yamlContent));

        SourceId = sourceId;
        Name = name;
        YamlContent = yamlContent;
        Description = description;
        Variables = (variables?.ToList() ?? new List<StackVariable>()).AsReadOnly();
        Services = (services?.ToList() ?? new List<string>()).AsReadOnly();
        FilePath = filePath;
        RelativePath = relativePath;
        AdditionalFiles = (additionalFiles?.ToList() ?? new List<string>()).AsReadOnly();
        AdditionalFileContents = new Dictionary<string, string>(additionalFileContents ?? new Dictionary<string, string>());
        LastSyncedAt = lastSyncedAt ?? SystemClock.UtcNow;
        Version = version;

        // Product properties - default to stack values if not specified
        ProductName = productName ?? name;
        ProductDisplayName = productDisplayName ?? name;
        ProductDescription = productDescription ?? description;
        ProductVersion = productVersion ?? version;
        Category = category;
        Tags = (tags?.ToList() ?? new List<string>()).AsReadOnly();

        // Maintenance configuration
        MaintenanceObserver = maintenanceObserver;
    }

    /// <summary>
    /// Gets all required variables (no default value or explicitly marked required).
    /// </summary>
    public IEnumerable<StackVariable> GetRequiredVariables()
    {
        return Variables.Where(v => v.IsRequired);
    }

    /// <summary>
    /// Gets all optional variables (have default value and not explicitly required).
    /// </summary>
    public IEnumerable<StackVariable> GetOptionalVariables()
    {
        return Variables.Where(v => !v.IsRequired);
    }

    /// <summary>
    /// Checks if this stack contains a service with the given name.
    /// </summary>
    public bool HasService(string serviceName)
    {
        return Services.Contains(serviceName);
    }
}
