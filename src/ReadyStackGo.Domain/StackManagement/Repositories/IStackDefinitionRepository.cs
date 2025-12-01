namespace ReadyStackGo.Domain.StackManagement.Repositories;

using ReadyStackGo.Domain.StackManagement.ValueObjects;

/// <summary>
/// Repository for reading stack definitions (loaded from sources).
/// This is a read-only repository as stack definitions are loaded from external sources.
/// </summary>
public interface IStackDefinitionRepository
{
    /// <summary>
    /// Gets a stack definition by ID (format: sourceId:stackName).
    /// </summary>
    Task<StackDefinition?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all stack definitions.
    /// </summary>
    Task<IReadOnlyList<StackDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all stack definitions from a specific source.
    /// </summary>
    Task<IReadOnlyList<StackDefinition>> GetBySourceAsync(StackSourceId sourceId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs stack definitions from all enabled sources.
    /// </summary>
    Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Syncs stack definitions from a specific source.
    /// </summary>
    Task<SyncResult> SyncSourceAsync(StackSourceId sourceId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of a sync operation.
/// </summary>
public record SyncResult
{
    public bool Success { get; init; }
    public int StacksLoaded { get; init; }
    public int SourcesSynced { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Warnings { get; init; } = Array.Empty<string>();

    public static SyncResult Successful(int stacksLoaded, int sourcesSynced) =>
        new() { Success = true, StacksLoaded = stacksLoaded, SourcesSynced = sourcesSynced };

    public static SyncResult Failed(params string[] errors) =>
        new() { Success = false, Errors = errors };
}
