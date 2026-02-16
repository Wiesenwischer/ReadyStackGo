using MediatR;

namespace ReadyStackGo.Application.UseCases.Containers.GetContainerContext;

public record GetContainerContextQuery(string EnvironmentId) : IRequest<GetContainerContextResult>;

public record StackContextInfo(
    string StackName,
    bool DeploymentExists,
    string? DeploymentId = null,
    string? ProductName = null,
    string? ProductDisplayName = null);

public record GetContainerContextResult(
    bool Success,
    IReadOnlyDictionary<string, StackContextInfo> Stacks,
    string? ErrorMessage = null)
{
    public static GetContainerContextResult Failed(string message) =>
        new(false, new Dictionary<string, StackContextInfo>(), message);
}
