using MediatR;

namespace ReadyStackGo.Application.UseCases.Volumes.CreateVolume;

public record CreateVolumeCommand(
    string EnvironmentId,
    string Name,
    string? Driver = null,
    IDictionary<string, string>? Labels = null) : IRequest<CreateVolumeResult>;

public record CreateVolumeResult(
    bool Success,
    VolumeDto? Volume = null,
    string? ErrorMessage = null);
