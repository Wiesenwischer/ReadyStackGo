using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Health.GetServiceHealth;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// GET /api/health/{environmentId}/deployments/{deploymentId}/services/{serviceName}
/// Get detailed health information for a specific service within a deployment.
/// Returns full health check entries when available.
/// </summary>
[RequirePermission("Health", "Read")]
public class GetServiceHealthEndpoint : Endpoint<GetServiceHealthRequest, GetServiceHealthResponse>
{
    private readonly IMediator _mediator;

    public GetServiceHealthEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/health/{environmentId}/deployments/{deploymentId}/services/{serviceName}");
        PreProcessor<RbacPreProcessor<GetServiceHealthRequest>>();
    }

    public override async Task HandleAsync(GetServiceHealthRequest req, CancellationToken ct)
    {
        var query = new GetServiceHealthQuery(
            req.EnvironmentId,
            req.DeploymentId,
            req.ServiceName,
            req.ForceRefresh);

        var response = await _mediator.Send(query, ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Service health data not available", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetServiceHealthRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string DeploymentId { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public bool ForceRefresh { get; set; }
}
