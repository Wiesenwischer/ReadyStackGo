namespace ReadyStackGo.Domain.StackManagement.ValueObjects;

/// <summary>
/// Types of stack sources.
/// </summary>
public enum StackSourceType
{
    /// <summary>
    /// Stack definitions from a local directory.
    /// </summary>
    LocalDirectory,

    /// <summary>
    /// Stack definitions from a Git repository.
    /// </summary>
    GitRepository
}
