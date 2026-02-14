namespace ReadyStackGo.Application.Services;

/// <summary>
/// Service for accessing the embedded source registry catalog.
/// </summary>
public interface ISourceRegistryService
{
    /// <summary>
    /// Returns all entries from the embedded source registry.
    /// </summary>
    IReadOnlyList<SourceRegistryEntry> GetAll();

    /// <summary>
    /// Returns a single registry entry by its ID, or null if not found.
    /// </summary>
    SourceRegistryEntry? GetById(string id);
}

/// <summary>
/// A curated stack source entry from the embedded registry.
/// Supports both git-repository and local-directory types.
/// </summary>
public record SourceRegistryEntry(
    string Id,
    string Name,
    string Description,
    string GitUrl,
    string GitBranch,
    string Category,
    IReadOnlyList<string> Tags,
    bool Featured,
    int StackCount,
    string Type = "git-repository",
    string? Path = null,
    string? FilePattern = null)
{
    public bool IsLocalDirectory => Type.Equals("local-directory", StringComparison.OrdinalIgnoreCase);
    public bool IsGitRepository => !IsLocalDirectory;
}
