using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Volumes;

namespace ReadyStackGo.Application.UseCases.Volumes.ListVolumes;

public class ListVolumesHandler : IRequestHandler<ListVolumesQuery, ListVolumesResult>
{
    private readonly IDockerService _dockerService;

    public ListVolumesHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<ListVolumesResult> Handle(ListVolumesQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var rawVolumes = await _dockerService.ListVolumesRawAsync(request.EnvironmentId, cancellationToken);
            var mounts = await _dockerService.GetContainerVolumeMountsAsync(request.EnvironmentId, cancellationToken);

            var volumeReferences = mounts
                .Select(m => new VolumeReference
                {
                    ContainerName = m.ContainerName,
                    VolumeName = m.VolumeName,
                    MountPath = m.MountPath
                })
                .ToList();

            var volumes = rawVolumes
                .Select(raw =>
                {
                    var volume = DockerVolume.FromDockerApi(
                        raw.Name,
                        raw.Driver,
                        raw.Mountpoint,
                        raw.Scope,
                        raw.CreatedAt,
                        raw.Labels);

                    var containers = volume.GetReferencingContainers(volumeReferences);

                    return new VolumeDto
                    {
                        Name = volume.Name,
                        Driver = volume.Driver,
                        Mountpoint = volume.Mountpoint,
                        Scope = volume.Scope,
                        CreatedAt = volume.CreatedAt,
                        Labels = volume.Labels,
                        ContainerCount = containers.Count,
                        ReferencedByContainers = containers,
                        IsOrphaned = volume.IsOrphaned(volumeReferences)
                    };
                })
                .OrderBy(v => v.Name)
                .ToList();

            return new ListVolumesResult(true, volumes);
        }
        catch (InvalidOperationException ex)
        {
            return new ListVolumesResult(false, [], ex.Message);
        }
    }
}
