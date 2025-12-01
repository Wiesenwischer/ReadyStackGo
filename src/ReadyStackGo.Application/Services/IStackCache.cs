using ReadyStackGo.Domain.StackManagement.ValueObjects;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Cache for stack definitions
/// </summary>
public interface IStackCache
{
    /// <summary>
    /// Get all cached stacks
    /// </summary>
    IEnumerable<StackDefinition> GetAll();

    /// <summary>
    /// Get stacks from a specific source
    /// </summary>
    IEnumerable<StackDefinition> GetBySource(string sourceId);

    /// <summary>
    /// Get a specific stack by ID
    /// </summary>
    StackDefinition? Get(string stackId);

    /// <summary>
    /// Add or update a stack in the cache
    /// </summary>
    void Set(StackDefinition stack);

    /// <summary>
    /// Add or update multiple stacks in the cache
    /// </summary>
    void SetMany(IEnumerable<StackDefinition> stacks);

    /// <summary>
    /// Remove a stack from the cache
    /// </summary>
    void Remove(string stackId);

    /// <summary>
    /// Remove all stacks from a specific source
    /// </summary>
    void RemoveBySource(string sourceId);

    /// <summary>
    /// Clear all cached stacks
    /// </summary>
    void Clear();

    /// <summary>
    /// Get the total number of cached stacks
    /// </summary>
    int Count { get; }
}
