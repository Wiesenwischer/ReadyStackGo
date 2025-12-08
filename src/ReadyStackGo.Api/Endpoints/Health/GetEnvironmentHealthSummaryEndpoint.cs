using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Health;
using ReadyStackGo.Application.UseCases.Health.GetEnvironmentHealthSummary;

namespace ReadyStackGo.API.Endpoints.Health;

/// <summary>
/// GET /api/health/{environmentId}
/// Get health summary for all deployments in an environment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator, Viewer (scoped).
/// </summary>
[RequirePermission("Health", "Read")]
public class GetEnvironmentHealthSummaryEndpoint : Endpoint<GetEnvironmentHealthSummaryRequest, GetEnvironmentHealthSummaryResponse>
{
    private readonly IMediator _mediator;

    public GetEnvironmentHealthSummaryEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/health/{environmentId}");
        PreProcessor<RbacPreProcessor<GetEnvironmentHealthSummaryRequest>>();
    }

    public override async Task HandleAsync(GetEnvironmentHealthSummaryRequest req, CancellationToken ct)
    {
        var query = new GetEnvironmentHealthSummaryQuery(req.EnvironmentId);
        var response = await _mediator.Send(query, ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Environment not found", StatusCodes.Status404NotFound);
        }

        Response = response;
    }
}

public class GetEnvironmentHealthSummaryRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}
