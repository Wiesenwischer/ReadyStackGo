using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.ListProductDeployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class ListProductDeploymentsRequest
{
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Lists product deployments in an environment. Requires Deployments.Read permission.
/// GET /api/environments/{environmentId}/product-deployments
/// </summary>
[RequirePermission("Deployments", "Read")]
public class ListProductDeploymentsEndpoint : Endpoint<ListProductDeploymentsRequest, ListProductDeploymentsResponse>
{
    private readonly IMediator _mediator;

    public ListProductDeploymentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/product-deployments");
        PreProcessor<RbacPreProcessor<ListProductDeploymentsRequest>>();
    }

    public override async Task HandleAsync(ListProductDeploymentsRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        req.EnvironmentId = environmentId;
        Response = await _mediator.Send(new ListProductDeploymentsQuery(environmentId), ct);
    }
}
