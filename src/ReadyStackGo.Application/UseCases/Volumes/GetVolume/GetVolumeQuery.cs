using MediatR;

namespace ReadyStackGo.Application.UseCases.Volumes.GetVolume;

public record GetVolumeQuery(string EnvironmentId, string VolumeName) : IRequest<GetVolumeResult>;

public record GetVolumeResult(
    bool Success,
    VolumeDto? Volume = null,
    string? ErrorMessage = null);
