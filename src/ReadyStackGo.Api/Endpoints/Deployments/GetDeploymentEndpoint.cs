using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.GetDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/environments/{environmentId}/deployments/{deploymentId} - Get a specific deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetDeploymentEndpoint : Endpoint<GetDeploymentRequest, GetDeploymentResponse>
{
    private readonly IMediator _mediator;

    public GetDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/deployments/{deploymentId}");
        PreProcessor<RbacPreProcessor<GetDeploymentRequest>>();
    }

    public override async Task HandleAsync(GetDeploymentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(new GetDeploymentByIdQuery(environmentId, deploymentId), ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Deployment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetDeploymentRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}
