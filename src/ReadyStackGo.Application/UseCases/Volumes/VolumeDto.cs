namespace ReadyStackGo.Application.UseCases.Volumes;

/// <summary>
/// API response DTO for a Docker volume.
/// </summary>
public record VolumeDto
{
    public required string Name { get; init; }
    public required string Driver { get; init; }
    public string? Mountpoint { get; init; }
    public string? Scope { get; init; }
    public DateTime? CreatedAt { get; init; }
    public IReadOnlyDictionary<string, string> Labels { get; init; } = new Dictionary<string, string>();
    public int ContainerCount { get; init; }
    public IReadOnlyList<string> ReferencedByContainers { get; init; } = [];
    public bool IsOrphaned { get; init; }

    /// <summary>
    /// Volume size in bytes. Only populated in detail/inspect responses (on-demand).
    /// </summary>
    public long? SizeBytes { get; init; }
}
