using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.UpgradeStack;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// POST /api/environments/{environmentId}/deployments/{deploymentId}/upgrade
/// Upgrades a deployment to a new version from the catalog.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Write")]
public class UpgradeStackEndpoint : Endpoint<UpgradeStackRequest, UpgradeStackResponse>
{
    private readonly IMediator _mediator;

    public UpgradeStackEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/deployments/{deploymentId}/upgrade");
        PreProcessor<RbacPreProcessor<UpgradeStackRequest>>();
    }

    public override async Task HandleAsync(UpgradeStackRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;

        if (string.IsNullOrEmpty(req.StackId))
        {
            ThrowError("StackId is required", StatusCodes.Status400BadRequest);
        }

        var response = await _mediator.Send(
            new UpgradeStackCommand(
                environmentId,
                deploymentId,
                req.StackId,
                req.Variables,
                req.SessionId), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Upgrade failed", statusCode);
        }

        Response = response;
    }
}

/// <summary>
/// Request for upgrading a deployment.
/// </summary>
public class UpgradeStackRequest
{
    /// <summary>
    /// Environment ID for RBAC validation.
    /// </summary>
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// Catalog stack ID of the new version (format: sourceId:productName:stackName).
    /// </summary>
    public string StackId { get; set; } = string.Empty;

    /// <summary>
    /// Optional variable overrides. Merged with existing deployment values.
    /// </summary>
    public Dictionary<string, string>? Variables { get; set; }

    /// <summary>
    /// Optional client session ID for SignalR progress tracking.
    /// </summary>
    public string? SessionId { get; set; }
}
