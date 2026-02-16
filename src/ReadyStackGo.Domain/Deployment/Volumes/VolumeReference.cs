namespace ReadyStackGo.Domain.Deployment.Volumes;

/// <summary>
/// Describes a container's mount reference to a Docker volume.
/// </summary>
public record VolumeReference
{
    public required string ContainerName { get; init; }
    public required string VolumeName { get; init; }
    public required string MountPath { get; init; }
}
