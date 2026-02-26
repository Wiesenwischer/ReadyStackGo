using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.RepairOrphanedStack;

public record RepairOrphanedStackCommand(string EnvironmentId, string StackName, string UserId)
    : IRequest<RepairOrphanedStackResult>;

public record RepairOrphanedStackResult(
    bool Success,
    string? DeploymentId = null,
    bool CatalogMatched = false,
    string? ErrorMessage = null);
