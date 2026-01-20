using ReadyStackGo.Domain.StackManagement.Sources;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for managing product sources and syncing products.
/// Products are the primary deployment unit - they contain one or more stacks
/// plus product-level configuration.
/// </summary>
public interface IProductSourceService
{
    /// <summary>
    /// Get all configured product sources
    /// </summary>
    Task<IEnumerable<StackSource>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new product source
    /// </summary>
    Task<StackSource> AddSourceAsync(StackSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a product source
    /// </summary>
    Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync all enabled sources
    /// </summary>
    Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync a specific source
    /// </summary>
    Task<SyncResult> SyncSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all products from the cache.
    /// </summary>
    Task<IEnumerable<ProductDefinition>> GetProductsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific product by ID (format: sourceId:productName)
    /// </summary>
    Task<ProductDefinition?> GetProductAsync(string productId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stacks across all products from the cache.
    /// </summary>
    Task<IEnumerable<StackDefinition>> GetStacksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific stack by ID (format: sourceId:productName:stackName)
    /// </summary>
    Task<StackDefinition?> GetStackAsync(string stackId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all versions of a product by its GroupId.
    /// Returns versions sorted by version number (newest first).
    /// </summary>
    Task<IEnumerable<ProductDefinition>> GetProductVersionsAsync(string groupId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get available upgrades for a product from the current version.
    /// Returns versions higher than currentVersion, sorted newest first.
    /// </summary>
    Task<IEnumerable<ProductDefinition>> GetAvailableUpgradesAsync(string groupId, string currentVersion, CancellationToken cancellationToken = default);
}
