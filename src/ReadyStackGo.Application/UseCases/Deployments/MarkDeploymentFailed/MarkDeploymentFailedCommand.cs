using MediatR;

namespace ReadyStackGo.Application.UseCases.Deployments.MarkDeploymentFailed;

/// <summary>
/// Command to manually mark a deployment as failed.
/// Used when a deployment is stuck in Installing/Upgrading status.
/// </summary>
public record MarkDeploymentFailedCommand(
    string EnvironmentId,
    string DeploymentId,
    string? Reason = null) : IRequest<MarkDeploymentFailedResponse>;

public record MarkDeploymentFailedResponse(
    bool Success,
    string? Message = null,
    string? PreviousStatus = null);
