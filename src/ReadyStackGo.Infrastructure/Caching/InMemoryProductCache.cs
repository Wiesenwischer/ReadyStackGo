using System.Collections.Concurrent;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Infrastructure.Caching;

/// <summary>
/// In-memory implementation of product cache with multi-version support.
/// Products are grouped by their GroupId, allowing multiple versions to coexist.
/// </summary>
public class InMemoryProductCache : IProductCache
{
    // Key: GroupId, Value: Dictionary of version -> ProductDefinition
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ProductDefinition>> _productGroups = new();

    public int ProductGroupCount => _productGroups.Count;

    public int ProductCount => _productGroups.Values.Sum(g => g.Count);

    public int StackCount => GetAllProducts().Sum(p => p.Stacks.Count);

    /// <summary>
    /// Get all cached products (returns latest version of each product group for backward compatibility)
    /// </summary>
    public IEnumerable<ProductDefinition> GetAllProducts()
    {
        return _productGroups.Values
            .Select(group => GetLatestVersion(group))
            .Where(p => p != null)
            .Cast<ProductDefinition>()
            .ToList();
    }

    /// <summary>
    /// Get products from a specific source (returns latest version of each product group)
    /// </summary>
    public IEnumerable<ProductDefinition> GetProductsBySource(string sourceId)
    {
        return GetAllProducts()
            .Where(p => p.SourceId == sourceId)
            .ToList();
    }

    /// <summary>
    /// Get a specific product by ID (format varies: old format sourceId:productName or new format sourceId:productName:version)
    /// </summary>
    public ProductDefinition? GetProduct(string productId)
    {
        // Try to find by exact ID first (new format with version)
        foreach (var group in _productGroups.Values)
        {
            var product = group.Values.FirstOrDefault(p => p.Id == productId);
            if (product != null)
                return product;
        }

        // Fallback: Try as GroupId (old format sourceId:productName) - return latest version
        if (_productGroups.TryGetValue(productId, out var versions))
        {
            return GetLatestVersion(versions);
        }

        return null;
    }

    /// <summary>
    /// Get all versions of a product by its GroupId.
    /// Returns versions sorted by version number (newest first).
    /// </summary>
    public IEnumerable<ProductDefinition> GetProductVersions(string groupId)
    {
        if (_productGroups.TryGetValue(groupId, out var versions))
        {
            return versions.Values
                .OrderByDescending(p => p.ProductVersion, new SemVerComparer())
                .ToList();
        }
        return Enumerable.Empty<ProductDefinition>();
    }

    /// <summary>
    /// Get a specific version of a product.
    /// </summary>
    public ProductDefinition? GetProductVersion(string groupId, string version)
    {
        if (_productGroups.TryGetValue(groupId, out var versions))
        {
            // Try exact version match
            if (versions.TryGetValue(version, out var product))
                return product;

            // Try normalized version (with/without 'v' prefix)
            var normalizedVersion = NormalizeVersion(version);
            return versions.Values.FirstOrDefault(p =>
                NormalizeVersion(p.ProductVersion) == normalizedVersion);
        }
        return null;
    }

    /// <summary>
    /// Get the latest version of a product by its GroupId.
    /// </summary>
    public ProductDefinition? GetLatestProductVersion(string groupId)
    {
        if (_productGroups.TryGetValue(groupId, out var versions))
        {
            return GetLatestVersion(versions);
        }
        return null;
    }

    /// <summary>
    /// Get all available versions for upgrade from the current version.
    /// Returns versions higher than currentVersion, sorted newest first.
    /// </summary>
    public IEnumerable<ProductDefinition> GetAvailableUpgrades(string groupId, string currentVersion)
    {
        if (!_productGroups.TryGetValue(groupId, out var versions))
            return Enumerable.Empty<ProductDefinition>();

        var comparer = new SemVerComparer();
        return versions.Values
            .Where(p => !string.IsNullOrEmpty(p.ProductVersion) &&
                        comparer.Compare(p.ProductVersion, currentVersion) > 0)
            .OrderByDescending(p => p.ProductVersion, comparer)
            .ToList();
    }

    /// <summary>
    /// Get all stacks across all products (latest versions only)
    /// </summary>
    public IEnumerable<StackDefinition> GetAllStacks()
    {
        return GetAllProducts().SelectMany(p => p.Stacks).ToList();
    }

    /// <summary>
    /// Get a specific stack by ID (format: sourceId:productName:stackName)
    /// </summary>
    public StackDefinition? GetStack(string stackId)
    {
        // Stack ID format: sourceId:productName:stackName (or with version: sourceId:productName:version:stackName)
        // We need to find the product first, then the stack within it
        foreach (var group in _productGroups.Values)
        {
            foreach (var product in group.Values)
            {
                var stack = product.Stacks.FirstOrDefault(s => s.Id == stackId);
                if (stack != null)
                {
                    return stack;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Add or update a product in the cache.
    /// Products are grouped by their GroupId - multiple versions can coexist.
    /// </summary>
    public void Set(ProductDefinition product)
    {
        var groupId = product.GroupId;
        var versionKey = product.ProductVersion ?? "latest";

        var versions = _productGroups.GetOrAdd(groupId, _ => new ConcurrentDictionary<string, ProductDefinition>());
        versions[versionKey] = product;
    }

    /// <summary>
    /// Add or update multiple products in the cache
    /// </summary>
    public void SetMany(IEnumerable<ProductDefinition> products)
    {
        foreach (var product in products)
        {
            Set(product);
        }
    }

    /// <summary>
    /// Remove a specific product version from the cache
    /// </summary>
    public void Remove(string productId)
    {
        // Try to find and remove the specific version
        foreach (var (groupId, versions) in _productGroups)
        {
            var toRemove = versions.Values.FirstOrDefault(p => p.Id == productId);
            if (toRemove != null)
            {
                var versionKey = toRemove.ProductVersion ?? "latest";
                versions.TryRemove(versionKey, out _);

                // If no versions left, remove the group
                if (versions.IsEmpty)
                {
                    _productGroups.TryRemove(groupId, out _);
                }
                return;
            }
        }

        // Fallback: Try as GroupId (old behavior) - remove all versions
        RemoveGroup(productId);
    }

    /// <summary>
    /// Remove all versions of a product group
    /// </summary>
    public void RemoveGroup(string groupId)
    {
        _productGroups.TryRemove(groupId, out _);
    }

    /// <summary>
    /// Remove all products from a specific source
    /// </summary>
    public void RemoveBySource(string sourceId)
    {
        var groupsToRemove = new List<string>();
        var versionsToRemove = new List<(string groupId, string versionKey)>();

        foreach (var (groupId, versions) in _productGroups)
        {
            foreach (var (versionKey, product) in versions)
            {
                if (product.SourceId == sourceId)
                {
                    versionsToRemove.Add((groupId, versionKey));
                }
            }
        }

        // Remove individual versions
        foreach (var (groupId, versionKey) in versionsToRemove)
        {
            if (_productGroups.TryGetValue(groupId, out var versions))
            {
                versions.TryRemove(versionKey, out _);

                // If no versions left, mark group for removal
                if (versions.IsEmpty)
                {
                    groupsToRemove.Add(groupId);
                }
            }
        }

        // Remove empty groups
        foreach (var groupId in groupsToRemove)
        {
            _productGroups.TryRemove(groupId, out _);
        }
    }

    /// <summary>
    /// Clear all cached products
    /// </summary>
    public void Clear()
    {
        _productGroups.Clear();
    }

    /// <summary>
    /// Get the latest version from a dictionary of versions.
    /// </summary>
    private static ProductDefinition? GetLatestVersion(ConcurrentDictionary<string, ProductDefinition> versions)
    {
        if (versions.IsEmpty)
            return null;

        return versions.Values
            .OrderByDescending(p => p.ProductVersion, new SemVerComparer())
            .FirstOrDefault();
    }

    /// <summary>
    /// Normalize version string for comparison (removes 'v' prefix).
    /// </summary>
    private static string NormalizeVersion(string? version)
    {
        if (string.IsNullOrEmpty(version))
            return string.Empty;

        return version.TrimStart('v', 'V');
    }

    /// <summary>
    /// Comparer for semantic version strings.
    /// </summary>
    private class SemVerComparer : IComparer<string?>
    {
        public int Compare(string? x, string? y)
        {
            if (string.IsNullOrEmpty(x) && string.IsNullOrEmpty(y)) return 0;
            if (string.IsNullOrEmpty(x)) return -1;
            if (string.IsNullOrEmpty(y)) return 1;

            var xNormalized = x.TrimStart('v', 'V');
            var yNormalized = y.TrimStart('v', 'V');

            // Try to parse as System.Version
            if (Version.TryParse(xNormalized, out var xVer) &&
                Version.TryParse(yNormalized, out var yVer))
            {
                return xVer.CompareTo(yVer);
            }

            // Fallback to string comparison
            return string.Compare(xNormalized, yNormalized, StringComparison.OrdinalIgnoreCase);
        }
    }
}
