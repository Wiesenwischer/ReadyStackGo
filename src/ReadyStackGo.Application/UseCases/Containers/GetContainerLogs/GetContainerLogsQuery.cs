using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.GetContainerLogs;

public record GetContainerLogsQuery(
    string EnvironmentId,
    string ContainerId,
    int? Tail = null) : IRequest<GetContainerLogsResult>;

public record GetContainerLogsResult(bool Success, string Logs, string? ErrorMessage = null);
