using MediatR;

namespace ReadyStackGo.Application.UseCases.Volumes.ListVolumes;

public record ListVolumesQuery(string EnvironmentId) : IRequest<ListVolumesResult>;

public record ListVolumesResult(
    bool Success,
    IReadOnlyList<VolumeDto> Volumes,
    string? ErrorMessage = null);
