namespace ReadyStackGo.Domain.StackManagement.Manifests;

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
