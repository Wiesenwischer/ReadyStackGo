using System.Collections.Concurrent;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.StackManagement.Stacks;

namespace ReadyStackGo.Infrastructure.Caching;

/// <summary>
/// In-memory implementation of product cache.
/// Products are keyed by their ID (sourceId:productName).
/// </summary>
public class InMemoryProductCache : IProductCache
{
    private readonly ConcurrentDictionary<string, ProductDefinition> _products = new();

    public int ProductCount => _products.Count;

    public int StackCount => _products.Values.Sum(p => p.Stacks.Count);

    public IEnumerable<ProductDefinition> GetAllProducts()
    {
        return _products.Values.ToList();
    }

    public IEnumerable<ProductDefinition> GetProductsBySource(string sourceId)
    {
        return _products.Values.Where(p => p.SourceId == sourceId).ToList();
    }

    public ProductDefinition? GetProduct(string productId)
    {
        _products.TryGetValue(productId, out var product);
        return product;
    }

    public IEnumerable<StackDefinition> GetAllStacks()
    {
        return _products.Values.SelectMany(p => p.Stacks).ToList();
    }

    public StackDefinition? GetStack(string stackId)
    {
        // Stack ID format: sourceId:productName:stackName
        // We need to find the product first, then the stack within it
        foreach (var product in _products.Values)
        {
            var stack = product.Stacks.FirstOrDefault(s => s.Id == stackId);
            if (stack != null)
            {
                return stack;
            }
        }
        return null;
    }

    public void Set(ProductDefinition product)
    {
        _products[product.Id] = product;
    }

    public void SetMany(IEnumerable<ProductDefinition> products)
    {
        foreach (var product in products)
        {
            _products[product.Id] = product;
        }
    }

    public void Remove(string productId)
    {
        _products.TryRemove(productId, out _);
    }

    public void RemoveBySource(string sourceId)
    {
        var keysToRemove = _products
            .Where(kvp => kvp.Value.SourceId == sourceId)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in keysToRemove)
        {
            _products.TryRemove(key, out _);
        }
    }

    public void Clear()
    {
        _products.Clear();
    }
}
