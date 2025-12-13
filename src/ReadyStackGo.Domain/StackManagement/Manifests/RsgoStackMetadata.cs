namespace ReadyStackGo.Domain.StackManagement.Manifests;

/// <summary>
/// Stack metadata for display and organization (used in multi-stack entries and fragments).
/// </summary>
public class RsgoStackMetadata
{
    /// <summary>
    /// Human-readable name of the stack.
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Description of what the stack does.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Category for organizing stacks (e.g., "Database", "Web", "Monitoring").
    /// </summary>
    public string? Category { get; set; }

    /// <summary>
    /// Tags for filtering and search.
    /// </summary>
    public List<string>? Tags { get; set; }
}
