using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.GetProductDeployment;
using ReadyStackGo.Application.UseCases.Deployments.GetProductDeploymentByProduct;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class GetProductDeploymentByProductRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}

/// <summary>
/// Gets the active product deployment by product group ID. Requires Deployments.Read permission.
/// GET /api/environments/{environmentId}/product-deployments/by-product/{groupId}
/// </summary>
[RequirePermission("Deployments", "Read")]
public class GetProductDeploymentByProductEndpoint : Endpoint<GetProductDeploymentByProductRequest, GetProductDeploymentResponse>
{
    private readonly IMediator _mediator;

    public GetProductDeploymentByProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/product-deployments/by-product/{groupId}");
        PreProcessor<RbacPreProcessor<GetProductDeploymentByProductRequest>>();
    }

    public override async Task HandleAsync(GetProductDeploymentByProductRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var groupId = Route<string>("groupId")!;
        req.EnvironmentId = environmentId;

        var response = await _mediator.Send(
            new GetProductDeploymentByProductQuery(environmentId, groupId), ct);

        if (response == null)
        {
            ThrowError("No active product deployment found for this product", StatusCodes.Status404NotFound);
            return;
        }

        Response = response;
    }
}
