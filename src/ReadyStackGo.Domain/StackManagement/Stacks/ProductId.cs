using ReadyStackGo.Domain.StackManagement.Manifests;

namespace ReadyStackGo.Domain.StackManagement.Stacks;

/// <summary>
/// Unique identifier for a product.
/// Either explicitly set in the manifest (metadata.productId) or derived from the product name.
/// </summary>
public record ProductId(string Value)
{
    /// <summary>
    /// Creates a ProductId from a manifest.
    /// Uses: explicit productId from metadata, or falls back to metadata name.
    /// The manifest's Metadata.Name must be set before calling this method.
    /// </summary>
    public static ProductId FromManifest(RsgoManifest manifest)
    {
        var metadata = manifest.Metadata;
        if (string.IsNullOrEmpty(metadata.ProductId) && string.IsNullOrEmpty(metadata.Name))
            throw new ArgumentException("Manifest must have either ProductId or Name set in metadata", nameof(manifest));

        return new ProductId(metadata.ProductId ?? metadata.Name!);
    }

    /// <summary>
    /// Creates a ProductId directly from a name.
    /// </summary>
    public static ProductId FromName(string name) => new(name);

    public override string ToString() => Value;
}
