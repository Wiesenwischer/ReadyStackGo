using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.RemoveDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class RemoveDeploymentRequest
{
    /// <summary>
    /// Environment ID for RBAC scope check (from route).
    /// </summary>
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// Session ID for SignalR progress notifications.
    /// </summary>
    public string? SessionId { get; set; }
}

/// <summary>
/// Removes a deployment. Requires Deployments.Delete permission.
/// DELETE /api/environments/{environmentId}/deployments/{deploymentId}
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Delete")]
public class RemoveDeploymentEndpoint : Endpoint<RemoveDeploymentRequest, DeployComposeResponse>
{
    private readonly IMediator _mediator;

    public RemoveDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/environments/{environmentId}/deployments/{deploymentId}");
        PreProcessor<RbacPreProcessor<RemoveDeploymentRequest>>();
    }

    public override async Task HandleAsync(RemoveDeploymentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(new RemoveDeploymentByIdCommand(environmentId, deploymentId, req.SessionId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to remove deployment");
        }

        Response = response;
    }
}
