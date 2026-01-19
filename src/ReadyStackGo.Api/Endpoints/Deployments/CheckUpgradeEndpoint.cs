using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.CheckUpgrade;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/environments/{environmentId}/deployments/{deploymentId}/upgrade/check
/// Checks if an upgrade is available for a deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class CheckUpgradeEndpoint : Endpoint<CheckUpgradeRequest, CheckUpgradeResponse>
{
    private readonly IMediator _mediator;

    public CheckUpgradeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/deployments/{deploymentId}/upgrade/check");
        PreProcessor<RbacPreProcessor<CheckUpgradeRequest>>();
    }

    public override async Task HandleAsync(CheckUpgradeRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;

        var response = await _mediator.Send(
            new CheckUpgradeQuery(environmentId, deploymentId), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Check failed", statusCode);
        }

        Response = response;
    }
}

/// <summary>
/// Empty request - all parameters come from route.
/// </summary>
public class CheckUpgradeRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}
