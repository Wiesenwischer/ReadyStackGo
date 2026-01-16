using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.GetDeploymentSnapshots;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/environments/{environmentId}/deployments/{deploymentId}/snapshots - Get all snapshots for a deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetDeploymentSnapshotsEndpoint : Endpoint<GetDeploymentSnapshotsRequest, GetDeploymentSnapshotsResponse>
{
    private readonly IMediator _mediator;

    public GetDeploymentSnapshotsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/deployments/{deploymentId}/snapshots");
        PreProcessor<RbacPreProcessor<GetDeploymentSnapshotsRequest>>();
    }

    public override async Task HandleAsync(GetDeploymentSnapshotsRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(new GetDeploymentSnapshotsQuery(environmentId, deploymentId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to get snapshots", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetDeploymentSnapshotsRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}
