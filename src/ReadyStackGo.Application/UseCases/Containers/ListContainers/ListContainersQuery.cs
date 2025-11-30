using MediatR;
using ReadyStackGo.Application.Services;

namespace ReadyStackGo.Application.UseCases.Containers.ListContainers;

public record ListContainersQuery(string EnvironmentId) : IRequest<ListContainersResult>;

public record ListContainersResult(bool Success, IEnumerable<ContainerDto> Containers, string? ErrorMessage = null);
