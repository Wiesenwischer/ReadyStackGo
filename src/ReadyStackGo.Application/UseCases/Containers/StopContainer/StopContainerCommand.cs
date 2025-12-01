using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.StopContainer;

public record StopContainerCommand(string EnvironmentId, string ContainerId) : IRequest<StopContainerResult>;

public record StopContainerResult(bool Success, string? ErrorMessage = null);
