using MediatR;
using ReadyStackGo.Application.Services;
using ReadyStackGo.Domain.Deployment.Volumes;

namespace ReadyStackGo.Application.UseCases.Volumes.CreateVolume;

public class CreateVolumeHandler : IRequestHandler<CreateVolumeCommand, CreateVolumeResult>
{
    private readonly IDockerService _dockerService;

    public CreateVolumeHandler(IDockerService dockerService)
    {
        _dockerService = dockerService;
    }

    public async Task<CreateVolumeResult> Handle(CreateVolumeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Name))
                return new CreateVolumeResult(false, ErrorMessage: "Volume name cannot be empty.");

            var raw = await _dockerService.CreateVolumeAsync(
                request.EnvironmentId,
                request.Name,
                request.Driver,
                request.Labels,
                cancellationToken);

            var volume = DockerVolume.FromDockerApi(
                raw.Name, raw.Driver, raw.Mountpoint, raw.Scope, raw.CreatedAt, raw.Labels);

            var dto = new VolumeDto
            {
                Name = volume.Name,
                Driver = volume.Driver,
                Mountpoint = volume.Mountpoint,
                Scope = volume.Scope,
                CreatedAt = volume.CreatedAt,
                Labels = volume.Labels
            };

            return new CreateVolumeResult(true, dto);
        }
        catch (InvalidOperationException ex)
        {
            return new CreateVolumeResult(false, ErrorMessage: ex.Message);
        }
    }
}
