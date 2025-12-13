namespace ReadyStackGo.Domain.StackManagement.Sources;

using ReadyStackGo.Domain.SharedKernel;


/// <summary>
/// Aggregate root representing a source of stack definitions.
/// Stack sources can be local directories or Git repositories.
/// </summary>
public class StackSource : AggregateRoot<StackSourceId>
{
    public string Name { get; private set; } = null!;
    public StackSourceType Type { get; private set; }
    public bool Enabled { get; private set; }
    public DateTime? LastSyncedAt { get; private set; }
    public DateTime CreatedAt { get; private set; }

    // Type-specific configuration
    public string? Path { get; private set; }
    public string? FilePattern { get; private set; }
    public string? GitUrl { get; private set; }
    public string? GitBranch { get; private set; }

    // For EF Core
    protected StackSource() { }

    private StackSource(
        StackSourceId id,
        string name,
        StackSourceType type)
    {
        SelfAssertArgumentNotNull(id, "StackSourceId is required.");
        SelfAssertArgumentNotEmpty(name, "Name is required.");

        Id = id;
        Name = name;
        Type = type;
        Enabled = true;
        CreatedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Creates a new local directory stack source.
    /// </summary>
    public static StackSource CreateLocalDirectory(
        StackSourceId id,
        string name,
        string path,
        string filePattern = "*.yml;*.yaml")
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required for local directory source.", nameof(path));

        var source = new StackSource(id, name, StackSourceType.LocalDirectory)
        {
            Path = path,
            FilePattern = filePattern
        };

        return source;
    }

    /// <summary>
    /// Creates a new Git repository stack source.
    /// </summary>
    public static StackSource CreateGitRepository(
        StackSourceId id,
        string name,
        string gitUrl,
        string? branch = "main",
        string? path = null,
        string filePattern = "*.yml;*.yaml")
    {
        if (string.IsNullOrWhiteSpace(gitUrl))
            throw new ArgumentException("Git URL is required for Git repository source.", nameof(gitUrl));

        var source = new StackSource(id, name, StackSourceType.GitRepository)
        {
            GitUrl = gitUrl,
            GitBranch = branch ?? "main",
            Path = path,
            FilePattern = filePattern
        };

        return source;
    }

    /// <summary>
    /// Enables this stack source.
    /// </summary>
    public void Enable()
    {
        Enabled = true;
    }

    /// <summary>
    /// Disables this stack source.
    /// </summary>
    public void Disable()
    {
        Enabled = false;
    }

    /// <summary>
    /// Updates the last sync time.
    /// </summary>
    public void MarkSynced()
    {
        LastSyncedAt = SystemClock.UtcNow;
    }

    /// <summary>
    /// Updates the name.
    /// </summary>
    public void UpdateName(string name)
    {
        SelfAssertArgumentNotEmpty(name, "Name is required.");
        Name = name;
    }

    public override string ToString() =>
        $"StackSource [id={Id}, name={Name}, type={Type}, enabled={Enabled}]";
}
