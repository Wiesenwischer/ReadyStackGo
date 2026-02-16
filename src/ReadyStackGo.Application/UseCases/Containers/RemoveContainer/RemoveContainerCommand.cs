using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.RemoveContainer;

public record RemoveContainerCommand(string EnvironmentId, string ContainerId, bool Force = false) : IRequest<RemoveContainerResult>;

public record RemoveContainerResult(bool Success, string? ErrorMessage = null);
