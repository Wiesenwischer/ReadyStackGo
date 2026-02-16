using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Volumes;

namespace ReadyStackGo.Application.UseCases.Volumes.RemoveVolume;

public class RemoveVolumeHandler : IRequestHandler<RemoveVolumeCommand, RemoveVolumeResult>
{
    private readonly IDockerService _dockerService;

    public RemoveVolumeHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<RemoveVolumeResult> Handle(RemoveVolumeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.VolumeName))
                return new RemoveVolumeResult(false, "Volume name cannot be empty.");

            if (!request.Force)
            {
                var mounts = await _dockerService.GetContainerVolumeMountsAsync(
                    request.EnvironmentId, cancellationToken);

                var references = mounts
                    .Select(m => new VolumeReference
                    {
                        ContainerName = m.ContainerName,
                        VolumeName = m.VolumeName,
                        MountPath = m.MountPath
                    })
                    .ToList();

                var isReferenced = references.Any(r =>
                    string.Equals(r.VolumeName, request.VolumeName, StringComparison.OrdinalIgnoreCase));

                if (isReferenced)
                {
                    return new RemoveVolumeResult(false,
                        $"Volume '{request.VolumeName}' is still referenced by containers. Use force to remove anyway.");
                }
            }

            await _dockerService.RemoveVolumeAsync(
                request.EnvironmentId, request.VolumeName, request.Force, cancellationToken);

            return new RemoveVolumeResult(true);
        }
        catch (InvalidOperationException ex)
        {
            return new RemoveVolumeResult(false, ex.Message);
        }
    }
}
