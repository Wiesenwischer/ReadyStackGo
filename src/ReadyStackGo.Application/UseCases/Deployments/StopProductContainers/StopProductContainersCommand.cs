using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.StopProductContainers;

/// <summary>
/// Command to stop all (or selected) containers of a product deployment.
/// Does NOT change the ProductDeployment state machine — this is a container-level operation.
/// </summary>
public record StopProductContainersCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    List<string>? StackNames = null
) : IRequest<StopProductContainersResponse>;
