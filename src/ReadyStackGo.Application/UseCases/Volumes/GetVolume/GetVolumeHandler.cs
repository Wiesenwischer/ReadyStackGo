using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Volumes;

namespace ReadyStackGo.Application.UseCases.Volumes.GetVolume;

public class GetVolumeHandler : IRequestHandler<GetVolumeQuery, GetVolumeResult>
{
    private readonly IDockerService _dockerService;

    public GetVolumeHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<GetVolumeResult> Handle(GetVolumeQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var raw = await _dockerService.InspectVolumeAsync(
                request.EnvironmentId, request.VolumeName, cancellationToken);

            var mounts = await _dockerService.GetContainerVolumeMountsAsync(
                request.EnvironmentId, cancellationToken);

            var volumeReferences = mounts
                .Select(m => new VolumeReference
                {
                    ContainerName = m.ContainerName,
                    VolumeName = m.VolumeName,
                    MountPath = m.MountPath
                })
                .ToList();

            var volume = DockerVolume.FromDockerApi(
                raw.Name, raw.Driver, raw.Mountpoint, raw.Scope, raw.CreatedAt, raw.Labels);

            var containers = volume.GetReferencingContainers(volumeReferences);

            var dto = new VolumeDto
            {
                Name = volume.Name,
                Driver = volume.Driver,
                Mountpoint = volume.Mountpoint,
                Scope = volume.Scope,
                CreatedAt = volume.CreatedAt,
                Labels = volume.Labels,
                ContainerCount = containers.Count,
                ReferencedByContainers = containers,
                IsOrphaned = volume.IsOrphaned(volumeReferences),
                SizeBytes = raw.SizeBytes
            };

            return new GetVolumeResult(true, dto);
        }
        catch (InvalidOperationException ex)
        {
            return new GetVolumeResult(false, ErrorMessage: ex.Message);
        }
    }
}
