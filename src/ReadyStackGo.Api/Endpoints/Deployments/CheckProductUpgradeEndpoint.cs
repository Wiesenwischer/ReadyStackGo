using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.CheckProductUpgrade;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// GET /api/environments/{environmentId}/product-deployments/{productDeploymentId}/upgrade/check
/// Checks if an upgrade is available for a product deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Read")]
public class CheckProductUpgradeEndpoint : Endpoint<CheckProductUpgradeRequest, CheckProductUpgradeResponse>
{
    private readonly IMediator _mediator;

    public CheckProductUpgradeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Get("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/upgrade/check");
        PreProcessor<RbacPreProcessor<CheckProductUpgradeRequest>>();
    }

    public override async Task HandleAsync(CheckProductUpgradeRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;

        var response = await _mediator.Send(
            new CheckProductUpgradeQuery(environmentId, productDeploymentId), ct);

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
/// Empty request — all parameters come from route.
/// </summary>
public class CheckProductUpgradeRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
}
