namespace ReadyStackGo.Domain.StackManagement.Sources;

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
    GitRepository,

    /// <summary>
    /// Stack definitions from an OCI container registry (Docker Hub, GHCR, Azure CR, etc.).
    /// </summary>
    OciRegistry
}
