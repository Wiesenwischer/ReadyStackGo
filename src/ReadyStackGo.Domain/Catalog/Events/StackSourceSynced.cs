namespace ReadyStackGo.Domain.Catalog.Events;

using ReadyStackGo.Domain.SharedKernel;

/// <summary>
/// Event raised when a stack source has been synced.
/// </summary>
public class StackSourceSynced : DomainEvent
{
    /// <summary>
    /// ID of the source that was synced.
    /// </summary>
    public string SourceId { get; }

    /// <summary>
    /// Name of the source.
    /// </summary>
    public string SourceName { get; }

    /// <summary>
    /// Number of stacks loaded from the source.
    /// </summary>
    public int StacksLoaded { get; }

    /// <summary>
    /// Whether the sync was successful.
    /// </summary>
    public bool Success { get; }

    /// <summary>
    /// Any errors that occurred during sync.
    /// </summary>
    public IReadOnlyList<string> Errors { get; }

    /// <summary>
    /// Any warnings generated during sync.
    /// </summary>
    public IReadOnlyList<string> Warnings { get; }

    public StackSourceSynced(
        string sourceId,
        string sourceName,
        int stacksLoaded,
        bool success,
        IReadOnlyList<string>? errors = null,
        IReadOnlyList<string>? warnings = null)
    {
        SourceId = sourceId;
        SourceName = sourceName;
        StacksLoaded = stacksLoaded;
        Success = success;
        Errors = errors ?? Array.Empty<string>();
        Warnings = warnings ?? Array.Empty<string>();
    }
}
