using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.MarkDeploymentFailed;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class MarkDeploymentFailedRequest
{
    /// <summary>
    /// Environment ID for RBAC scope check (from route).
    /// </summary>
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// Optional reason for marking as failed.
    /// </summary>
    public string? Reason { get; set; }
}

/// <summary>
/// Marks a deployment as failed. Used for stuck deployments in Installing/Upgrading status.
/// POST /api/environments/{environmentId}/deployments/{deploymentId}/mark-failed
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Update")]
public class MarkDeploymentFailedEndpoint : Endpoint<MarkDeploymentFailedRequest, MarkDeploymentFailedResponse>
{
    private readonly IMediator _mediator;

    public MarkDeploymentFailedEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/deployments/{deploymentId}/mark-failed");
        PreProcessor<RbacPreProcessor<MarkDeploymentFailedRequest>>();
    }

    public override async Task HandleAsync(MarkDeploymentFailedRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(
            new MarkDeploymentFailedCommand(environmentId, deploymentId, req.Reason),
            ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to mark deployment as failed");
        }

        Response = response;
    }
}
