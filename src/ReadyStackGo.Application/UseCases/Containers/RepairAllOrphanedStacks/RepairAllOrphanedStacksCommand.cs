using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.RepairAllOrphanedStacks;

public record RepairAllOrphanedStacksCommand(string EnvironmentId, string UserId)
    : IRequest<RepairAllOrphanedStacksResult>;

public record RepairAllOrphanedStacksResult(
    bool Success,
    int RepairedCount,
    int FailedCount,
    string? ErrorMessage = null);
