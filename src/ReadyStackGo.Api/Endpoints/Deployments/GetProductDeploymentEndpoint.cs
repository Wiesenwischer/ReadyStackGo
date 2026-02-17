using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class GetProductDeploymentRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}

/// <summary>
/// Gets a product deployment by ID. Requires Deployments.Read permission.
/// GET /api/environments/{environmentId}/product-deployments/{id}
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetProductDeploymentEndpoint : Endpoint<GetProductDeploymentRequest, GetProductDeploymentResponse>
{
    private readonly IMediator _mediator;

    public GetProductDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/product-deployments/{id}");
        PreProcessor<RbacPreProcessor<GetProductDeploymentRequest>>();
    }

    public override async Task HandleAsync(GetProductDeploymentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var id = Route<string>("id")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(new GetProductDeploymentQuery(environmentId, id), ct);

        if (response == null)
        {
            ThrowError("Product deployment not found", StatusCodes.Status404NotFound);
            return;
        }

        Response = response;
    }
}
