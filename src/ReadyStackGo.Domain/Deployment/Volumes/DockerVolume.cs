namespace ReadyStackGo.Domain.Deployment.Volumes;

/// <summary>
/// Represents a Docker volume as a domain entity.
/// Not persisted in the database — constructed from Docker API data.
/// </summary>
public class DockerVolume
{
    public string Name { get; }
    public string Driver { get; }
    public string? Mountpoint { get; }
    public string? Scope { get; }
    public DateTime? CreatedAt { get; }
    public IReadOnlyDictionary<string, string> Labels { get; }

    private DockerVolume(
        string name,
        string driver,
        string? mountpoint,
        string? scope,
        DateTime? createdAt,
        IReadOnlyDictionary<string, string> labels)
    {
        Name = name;
        Driver = driver;
        Mountpoint = mountpoint;
        Scope = scope;
        CreatedAt = createdAt;
        Labels = labels;
    }

    /// <summary>
    /// Creates a DockerVolume from raw Docker API data.
    /// </summary>
    public static DockerVolume FromDockerApi(
        string name,
        string driver,
        string? mountpoint = null,
        string? scope = null,
        DateTime? createdAt = null,
        IDictionary<string, string>? labels = null)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Volume name cannot be empty.", nameof(name));

        return new DockerVolume(
            name,
            driver ?? "local",
            mountpoint,
            scope,
            createdAt,
            labels != null
                ? new Dictionary<string, string>(labels)
                : new Dictionary<string, string>());
    }

    /// <summary>
    /// Determines whether this volume is orphaned (not referenced by any container).
    /// </summary>
    public bool IsOrphaned(IReadOnlyList<VolumeReference> allReferences)
    {
        return !allReferences.Any(r =>
            string.Equals(r.VolumeName, Name, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    /// Returns all containers that reference this volume.
    /// </summary>
    public IReadOnlyList<string> GetReferencingContainers(IReadOnlyList<VolumeReference> allReferences)
    {
        return allReferences
            .Where(r => string.Equals(r.VolumeName, Name, StringComparison.OrdinalIgnoreCase))
            .Select(r => r.ContainerName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
