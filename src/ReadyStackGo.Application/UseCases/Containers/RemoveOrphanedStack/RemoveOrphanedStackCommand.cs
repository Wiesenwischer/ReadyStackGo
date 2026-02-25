using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.RemoveOrphanedStack;

public record RemoveOrphanedStackCommand(string EnvironmentId, string StackName)
    : IRequest<RemoveOrphanedStackResult>;

public record RemoveOrphanedStackResult(bool Success, int RemovedCount, string? ErrorMessage = null);
