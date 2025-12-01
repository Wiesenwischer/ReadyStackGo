using System.Text.Json.Serialization;

namespace ReadyStackGo.Infrastructure.Stacks.Configuration;

/// <summary>
/// Configuration file structure for stack sources (rsgo.stacks.json).
/// </summary>
internal class StackSourceConfig
{
    public List<StackSourceEntry> Sources { get; set; } = new();
}

/// <summary>
/// Stack source entry in the configuration file.
/// Uses discriminator-based polymorphism for different source types.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(LocalDirectorySourceEntry), "local-directory")]
[JsonDerivedType(typeof(GitRepositorySourceEntry), "git-repository")]
internal abstract class StackSourceEntry
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public bool Enabled { get; set; } = true;
    public DateTime? LastSyncedAt { get; set; }
}

/// <summary>
/// Configuration entry for a local directory stack source.
/// </summary>
internal class LocalDirectorySourceEntry : StackSourceEntry
{
    public required string Path { get; set; }
    public string FilePattern { get; set; } = "*.yml;*.yaml";
}

/// <summary>
/// Configuration entry for a Git repository stack source.
/// </summary>
internal class GitRepositorySourceEntry : StackSourceEntry
{
    public required string GitUrl { get; set; }
    public string Branch { get; set; } = "main";
    public string? Path { get; set; }
    public string FilePattern { get; set; } = "*.yml;*.yaml";
}
