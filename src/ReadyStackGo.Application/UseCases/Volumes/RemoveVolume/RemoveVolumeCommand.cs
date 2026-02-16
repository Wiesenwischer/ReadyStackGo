using MediatR;

namespace ReadyStackGo.Application.UseCases.Volumes.RemoveVolume;

public record RemoveVolumeCommand(
    string EnvironmentId,
    string VolumeName,
    bool Force = false) : IRequest<RemoveVolumeResult>;

public record RemoveVolumeResult(
    bool Success,
    string? ErrorMessage = null);
