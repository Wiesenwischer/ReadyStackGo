using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Application.UseCases.Health.GetHealthHistory;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// GET /api/health/deployments/{deploymentId}/history
/// Get health history for a specific deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Health", "Read")]
public class GetHealthHistoryEndpoint : Endpoint<GetHealthHistoryRequest, GetHealthHistoryResponse>
{
    private readonly IMediator _mediator;

    public GetHealthHistoryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/health/deployments/{deploymentId}/history");
        PreProcessor<RbacPreProcessor<GetHealthHistoryRequest>>();
    }

    public override async Task HandleAsync(GetHealthHistoryRequest req, CancellationToken ct)
    {
        var query = new GetHealthHistoryQuery(req.DeploymentId, req.Limit);
        var response = await _mediator.Send(query, ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Deployment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetHealthHistoryRequest
{
    public string DeploymentId { get; set; } = string.Empty;

    /// <summary>
    /// Maximum number of history entries to return. Default is 10.
    /// </summary>
    public int Limit { get; set; } = 10;
}
