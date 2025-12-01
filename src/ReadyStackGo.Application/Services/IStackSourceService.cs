using ReadyStackGo.Domain.StackManagement.Aggregates;
using ReadyStackGo.Domain.StackManagement.ValueObjects;

namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for managing stack sources and syncing stacks
/// </summary>
public interface IStackSourceService
{
    /// <summary>
    /// Get all configured stack sources
    /// </summary>
    Task<IEnumerable<StackSource>> GetSourcesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Add a new stack source
    /// </summary>
    Task<StackSource> AddSourceAsync(StackSource source, CancellationToken cancellationToken = default);

    /// <summary>
    /// Remove a stack source
    /// </summary>
    Task RemoveSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync all enabled sources
    /// </summary>
    Task<Domain.StackManagement.Repositories.SyncResult> SyncAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Sync a specific source
    /// </summary>
    Task<Domain.StackManagement.Repositories.SyncResult> SyncSourceAsync(string sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all stacks from the cache
    /// </summary>
    Task<IEnumerable<StackDefinition>> GetStacksAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific stack by ID
    /// </summary>
    Task<StackDefinition?> GetStackAsync(string stackId, CancellationToken cancellationToken = default);
}
