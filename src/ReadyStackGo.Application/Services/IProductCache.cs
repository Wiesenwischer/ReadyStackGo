using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Cache for product definitions.
/// Products are the primary deployment unit - they contain stacks + product-level config.
/// Supports multiple versions per product, grouped by GroupId.
/// </summary>
public interface IProductCache
{
    /// <summary>
    /// Get all cached products (returns latest version of each product group for backward compatibility)
    /// </summary>
    IEnumerable<ProductDefinition> GetAllProducts();

    /// <summary>
    /// Get products from a specific source (returns latest version of each product group)
    /// </summary>
    IEnumerable<ProductDefinition> GetProductsBySource(string sourceId);

    /// <summary>
    /// Get a specific product by ID (format: sourceId:productName or sourceId:productName:version)
    /// </summary>
    ProductDefinition? GetProduct(string productId);

    /// <summary>
    /// Get all versions of a product by its GroupId.
    /// Returns versions sorted by version number (newest first).
    /// </summary>
    IEnumerable<ProductDefinition> GetProductVersions(string groupId);

    /// <summary>
    /// Get a specific version of a product.
    /// </summary>
    ProductDefinition? GetProductVersion(string groupId, string version);

    /// <summary>
    /// Get the latest version of a product by its GroupId.
    /// </summary>
    ProductDefinition? GetLatestProductVersion(string groupId);

    /// <summary>
    /// Get all available versions for upgrade from the current version.
    /// Returns versions higher than currentVersion, sorted newest first.
    /// </summary>
    IEnumerable<ProductDefinition> GetAvailableUpgrades(string groupId, string currentVersion);

    /// <summary>
    /// Get all stacks across all products (latest versions only)
    /// </summary>
    IEnumerable<StackDefinition> GetAllStacks();

    /// <summary>
    /// Get a specific stack by ID (format: sourceId:productName:stackName)
    /// </summary>
    StackDefinition? GetStack(string stackId);

    /// <summary>
    /// Add or update a product in the cache.
    /// Products are grouped by their GroupId - multiple versions can coexist.
    /// </summary>
    void Set(ProductDefinition product);

    /// <summary>
    /// Add or update multiple products in the cache
    /// </summary>
    void SetMany(IEnumerable<ProductDefinition> products);

    /// <summary>
    /// Remove a specific product version from the cache
    /// </summary>
    void Remove(string productId);

    /// <summary>
    /// Remove all versions of a product group
    /// </summary>
    void RemoveGroup(string groupId);

    /// <summary>
    /// Remove all products from a specific source
    /// </summary>
    void RemoveBySource(string sourceId);

    /// <summary>
    /// Clear all cached products
    /// </summary>
    void Clear();

    /// <summary>
    /// Get the total number of unique product groups
    /// </summary>
    int ProductGroupCount { get; }

    /// <summary>
    /// Get the total number of product versions across all groups
    /// </summary>
    int ProductCount { get; }

    /// <summary>
    /// Get the total number of stacks across all products (latest versions)
    /// </summary>
    int StackCount { get; }
}
