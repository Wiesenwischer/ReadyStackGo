using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.StartContainer;

public record StartContainerCommand(string EnvironmentId, string ContainerId) : IRequest<StartContainerResult>;

public record StartContainerResult(bool Success, string? ErrorMessage = null);
