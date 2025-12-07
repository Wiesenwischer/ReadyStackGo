namespace ReadyStackGo.Domain.StackManagement.StackSources;

/// <summary>
/// A product is the primary deployment unit in ReadyStackGo.
/// A product can contain one or more stacks.
///
/// Examples:
/// - WordPress (single-stack product): One stack with wordpress + mysql services
/// - AMS (multi-stack product): Multiple stacks like IdentityAccess, Infrastructure, etc.
/// </summary>
public class ProductDefinition
{
    /// <summary>
    /// Unique identifier (format: sourceId:productName).
    /// </summary>
    public string Id => $"{SourceId}:{Name}";

    /// <summary>
    /// ID of the source this product came from.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Name of the product (e.g., "WordPress", "ams.project").
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Human-readable display name.
    /// </summary>
    public string DisplayName { get; }

    /// <summary>
    /// Product description.
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Product version (from metadata.productVersion).
    /// </summary>
    public string? ProductVersion { get; }

    /// <summary>
    /// Category for organizing products (e.g., "Database", "CMS", "Enterprise").
    /// </summary>
    public string? Category { get; }

    /// <summary>
    /// Tags for filtering and search.
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// Icon URL for UI display.
    /// </summary>
    public string? Icon { get; }

    /// <summary>
    /// Documentation URL.
    /// </summary>
    public string? Documentation { get; }

    /// <summary>
    /// Whether this product contains multiple stacks.
    /// </summary>
    public bool IsMultiStack => Stacks.Count > 1;

    /// <summary>
    /// The stacks contained in this product.
    /// Single-stack products have exactly one stack.
    /// </summary>
    public IReadOnlyList<StackDefinition> Stacks { get; }

    /// <summary>
    /// Total number of services across all stacks.
    /// </summary>
    public int TotalServices => Stacks.Sum(s => s.Services.Count);

    /// <summary>
    /// Total number of configurable variables across all stacks.
    /// </summary>
    public int TotalVariables => Stacks.Sum(s => s.Variables.Count);

    /// <summary>
    /// When any stack in this product was last synced.
    /// </summary>
    public DateTime LastSyncedAt => Stacks.Max(s => s.LastSyncedAt);

    public ProductDefinition(
        string sourceId,
        string name,
        string displayName,
        IEnumerable<StackDefinition> stacks,
        string? description = null,
        string? productVersion = null,
        string? category = null,
        IEnumerable<string>? tags = null,
        string? icon = null,
        string? documentation = null)
    {
        if (string.IsNullOrWhiteSpace(sourceId))
            throw new ArgumentException("SourceId cannot be empty.", nameof(sourceId));
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Name cannot be empty.", nameof(name));

        var stackList = stacks?.ToList() ?? new List<StackDefinition>();
        if (stackList.Count == 0)
            throw new ArgumentException("Product must contain at least one stack.", nameof(stacks));

        SourceId = sourceId;
        Name = name;
        DisplayName = displayName;
        Stacks = stackList.AsReadOnly();
        Description = description;
        ProductVersion = productVersion;
        Category = category;
        Tags = (tags?.ToList() ?? new List<string>()).AsReadOnly();
        Icon = icon;
        Documentation = documentation;
    }
}
