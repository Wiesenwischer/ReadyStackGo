using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.RestartProductContainers;

/// <summary>
/// Command to restart all (or selected) containers of a product deployment.
/// Restart = Stop + Start sequentially per stack.
/// Does NOT change the ProductDeployment state machine — this is a container-level operation.
/// </summary>
public record RestartProductContainersCommand(
    string EnvironmentId,
    string ProductDeploymentId,
    List<string>? StackNames = null
) : IRequest<RestartProductContainersResponse>;
