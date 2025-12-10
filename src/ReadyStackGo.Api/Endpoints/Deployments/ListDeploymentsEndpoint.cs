using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments;
using ReadyStackGo.Application.UseCases.Deployments.ListDeployments;

namespace ReadyStackGo.API.Endpoints.Deployments;

public class ListDeploymentsRequest
{
    /// <summary>
    /// Environment ID for RBAC scope check (from route).
    /// </summary>
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Lists deployments in an environment. Requires Deployments.Read permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class ListDeploymentsEndpoint : Endpoint<ListDeploymentsRequest, ListDeploymentsResponse>
{
    private readonly IMediator _mediator;

    public ListDeploymentsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/deployments");
        PreProcessor<RbacPreProcessor<ListDeploymentsRequest>>();
    }

    public override async Task HandleAsync(ListDeploymentsRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        req.EnvironmentId = environmentId;
        Response = await _mediator.Send(new ListDeploymentsQuery(environmentId), ct);
    }
}
