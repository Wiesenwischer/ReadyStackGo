namespace ReadyStackGo.Domain.StackManagement.Stacks;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// A stack definition loaded from a stack source.
/// Contains structured data about services, volumes, networks, and variables.
/// NO YAML content - all data is parsed into structured domain objects.
/// </summary>
public class StackDefinition
{
    /// <summary>
    /// Unique identifier for this stack.
    /// </summary>
    public StackId Id { get; }

    /// <summary>
    /// ID of the source this stack came from.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Name of the stack.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Stack description. Uses Null Object Pattern - never null, use Description.Empty for no description.
    /// </summary>
    public Description Description { get; }

    /// <summary>
    /// Variables (environment variables) for this stack.
    /// </summary>
    public IReadOnlyList<Variable> Variables { get; }

    /// <summary>
    /// Service templates defining the containers in this stack.
    /// </summary>
    public IReadOnlyList<ServiceTemplate> Services { get; }

    /// <summary>
    /// Named volumes defined at stack level.
    /// </summary>
    public IReadOnlyList<VolumeDefinition> Volumes { get; }

    /// <summary>
    /// Networks defined at stack level.
    /// </summary>
    public IReadOnlyList<NetworkDefinition> Networks { get; }

    /// <summary>
    /// Path to the source file (for display purposes).
    /// </summary>
    public string? FilePath { get; }

    /// <summary>
    /// Relative path from the stack source root.
    /// </summary>
    public string? RelativePath { get; }

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
    /// Unique identifier for the product this stack belongs to.
    /// </summary>
    public ProductId ProductId { get; }

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
    public Description ProductDescription { get; }

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

    public StackDefinition(
        string sourceId,
        string name,
        ProductId productId,
        IEnumerable<ServiceTemplate>? services = null,
        Description? description = null,
        IEnumerable<Variable>? variables = null,
        IEnumerable<VolumeDefinition>? volumes = null,
        IEnumerable<NetworkDefinition>? networks = null,
        string? filePath = null,
        string? relativePath = null,
        DateTime? lastSyncedAt = null,
        string? version = null,
        // Product properties
        string? productName = null,
        string? productDisplayName = null,
        Description? productDescription = null,
        string? productVersion = null,
        string? category = null,
        IEnumerable<string>? tags = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        SourceId = sourceId;
        Name = name;
        ProductId = productId;
        Description = description ?? Description.Empty;
        Variables = (variables?.ToList() ?? new List<Variable>()).AsReadOnly();
        Services = (services?.ToList() ?? new List<ServiceTemplate>()).AsReadOnly();
        Volumes = (volumes?.ToList() ?? new List<VolumeDefinition>()).AsReadOnly();
        Networks = (networks?.ToList() ?? new List<NetworkDefinition>()).AsReadOnly();
        FilePath = filePath;
        RelativePath = relativePath;
        LastSyncedAt = lastSyncedAt ?? SystemClock.UtcNow;
        Version = version;

        // Product properties - default to stack values if not specified
        ProductName = productName ?? name;
        ProductDisplayName = productDisplayName ?? name;
        ProductDescription = productDescription ?? Description.Empty;
        ProductVersion = productVersion ?? version;
        Category = category;
        Tags = (tags?.ToList() ?? new List<string>()).AsReadOnly();

        // Stack ID is composed of its identifying components
        Id = new StackId(sourceId, productId, ProductVersion, name);
    }

    /// <summary>
    /// Gets all required variables (no default value or explicitly marked required).
    /// </summary>
    public IEnumerable<Variable> GetRequiredVariables()
    {
        return Variables.Where(v => v.IsRequired);
    }

    /// <summary>
    /// Gets all optional variables (have default value and not explicitly required).
    /// </summary>
    public IEnumerable<Variable> GetOptionalVariables()
    {
        return Variables.Where(v => !v.IsRequired);
    }

    /// <summary>
    /// Checks if this stack contains a service with the given name.
    /// </summary>
    public bool HasService(string serviceName)
    {
        return Services.Any(s => s.Name == serviceName);
    }

    /// <summary>
    /// Gets the service names as a list of strings.
    /// </summary>
    public IEnumerable<string> GetServiceNames()
    {
        return Services.Select(s => s.Name);
    }

    /// <summary>
    /// Gets a service by name.
    /// </summary>
    public ServiceTemplate? GetService(string serviceName)
    {
        return Services.FirstOrDefault(s => s.Name == serviceName);
    }
}
