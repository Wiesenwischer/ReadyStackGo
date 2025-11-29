using System.Text.Json.Serialization;

namespace ReadyStackGo.Domain.Stacks;

/// <summary>
/// Base class for stack sources. A stack source provides stack definitions from various locations.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
[JsonDerivedType(typeof(LocalDirectoryStackSource), "local-directory")]
[JsonDerivedType(typeof(GitRepositoryStackSource), "git-repository")]
[JsonDerivedType(typeof(CompositeStackSource), "composite")]
public abstract class StackSource
{
    /// <summary>
    /// Unique identifier for this source
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Display name for this source
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Whether this source is enabled
    /// </summary>
    public bool Enabled { get; init; } = true;

    /// <summary>
    /// Last time this source was synced
    /// </summary>
    public DateTime? LastSyncedAt { get; set; }
}

/// <summary>
/// Stack source that reads from a local directory
/// </summary>
public class LocalDirectoryStackSource : StackSource
{
    /// <summary>
    /// Path to the directory containing stack YAML files
    /// </summary>
    public required string Path { get; init; }

    /// <summary>
    /// File pattern to match (default: *.yml, *.yaml)
    /// </summary>
    public string FilePattern { get; init; } = "*.yml;*.yaml";
}

/// <summary>
/// Stack source that reads from a Git repository
/// </summary>
public class GitRepositoryStackSource : StackSource
{
    /// <summary>
    /// URL of the Git repository
    /// </summary>
    public required string Url { get; init; }

    /// <summary>
    /// Branch or tag to use (default: main)
    /// </summary>
    public string Branch { get; init; } = "main";

    /// <summary>
    /// Path within the repository to read stacks from (default: root)
    /// </summary>
    public string Path { get; init; } = "";

    /// <summary>
    /// File pattern to match (default: *.yml, *.yaml)
    /// </summary>
    public string FilePattern { get; init; } = "*.yml;*.yaml";

    /// <summary>
    /// Optional authentication token for private repositories
    /// </summary>
    public string? AuthToken { get; init; }
}

/// <summary>
/// Stack source that combines multiple other sources
/// </summary>
public class CompositeStackSource : StackSource
{
    /// <summary>
    /// Child sources to combine
    /// </summary>
    public List<StackSource> Sources { get; init; } = new();
}
