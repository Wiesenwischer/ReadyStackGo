using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Cache for product definitions.
/// Products are the primary deployment unit - they contain stacks + product-level config.
/// </summary>
public interface IProductCache
{
    /// <summary>
    /// Get all cached products
    /// </summary>
    IEnumerable<ProductDefinition> GetAllProducts();

    /// <summary>
    /// Get products from a specific source
    /// </summary>
    IEnumerable<ProductDefinition> GetProductsBySource(string sourceId);

    /// <summary>
    /// Get a specific product by ID (format: sourceId:productName)
    /// </summary>
    ProductDefinition? GetProduct(string productId);

    /// <summary>
    /// Get all stacks across all products
    /// </summary>
    IEnumerable<StackDefinition> GetAllStacks();

    /// <summary>
    /// Get a specific stack by ID (format: sourceId:productName:stackName)
    /// </summary>
    StackDefinition? GetStack(string stackId);

    /// <summary>
    /// Add or update a product in the cache
    /// </summary>
    void Set(ProductDefinition product);

    /// <summary>
    /// Add or update multiple products in the cache
    /// </summary>
    void SetMany(IEnumerable<ProductDefinition> products);

    /// <summary>
    /// Remove a product from the cache
    /// </summary>
    void Remove(string productId);

    /// <summary>
    /// Remove all products from a specific source
    /// </summary>
    void RemoveBySource(string sourceId);

    /// <summary>
    /// Clear all cached products
    /// </summary>
    void Clear();

    /// <summary>
    /// Get the total number of cached products
    /// </summary>
    int ProductCount { get; }

    /// <summary>
    /// Get the total number of stacks across all products
    /// </summary>
    int StackCount { get; }
}
