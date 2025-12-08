using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Application.UseCases.Health.GetStackHealth;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// GET /api/health/{environmentId}/deployments/{deploymentId}
/// Get detailed health information for a specific stack deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Health", "Read")]
public class GetStackHealthEndpoint : Endpoint<GetStackHealthRequest, GetStackHealthResponse>
{
    private readonly IMediator _mediator;

    public GetStackHealthEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/health/{environmentId}/deployments/{deploymentId}");
        PreProcessor<RbacPreProcessor<GetStackHealthRequest>>();
    }

    public override async Task HandleAsync(GetStackHealthRequest req, CancellationToken ct)
    {
        var query = new GetStackHealthQuery(
            req.EnvironmentId,
            req.DeploymentId,
            req.ForceRefresh);

        var response = await _mediator.Send(query, ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Health data not available", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetStackHealthRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;

    /// <summary>
    /// When true, forces a fresh health check instead of using cached data.
    /// </summary>
    public bool ForceRefresh { get; set; }
}
