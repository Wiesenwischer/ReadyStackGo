using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Application.UseCases.Health.GetHealthTransitions;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// GET /api/health/deployments/{deploymentId}/transitions
/// Get health status transitions for a specific deployment.
/// Returns only snapshots where the overall status changed.
/// </summary>
[RequirePermission("Health", "Read")]
public class GetHealthTransitionsEndpoint : Endpoint<GetHealthTransitionsRequest, GetHealthTransitionsResponse>
{
    private readonly IMediator _mediator;

    public GetHealthTransitionsEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/health/deployments/{deploymentId}/transitions");
        PreProcessor<RbacPreProcessor<GetHealthTransitionsRequest>>();
    }

    public override async Task HandleAsync(GetHealthTransitionsRequest req, CancellationToken ct)
    {
        var query = new GetHealthTransitionsQuery(req.DeploymentId);
        var response = await _mediator.Send(query, ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Deployment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetHealthTransitionsRequest
{
    public string DeploymentId { get; set; } = string.Empty;
}
