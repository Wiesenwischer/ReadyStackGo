using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// POST /api/environments/{environmentId}/deployments/{deploymentId}/rollback - Rollback a deployment to previous version.
/// Rollback is only available after a failed upgrade (before Point of No Return).
/// No SnapshotId required - always rolls back to the single PendingUpgradeSnapshot.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Write")]
public class RollbackDeploymentEndpoint : Endpoint<RollbackDeploymentRequest, RollbackDeploymentResponse>
{
    private readonly IMediator _mediator;
    private readonly ILogger<RollbackDeploymentEndpoint> _logger;

    public RollbackDeploymentEndpoint(IMediator mediator, ILogger<RollbackDeploymentEndpoint> logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/deployments/{deploymentId}/rollback");
        PreProcessor<RbacPreProcessor<RollbackDeploymentRequest>>();
    }

    public override async Task HandleAsync(RollbackDeploymentRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var deploymentId = Route<string>("deploymentId")!;
        req.EnvironmentId = environmentId;

        _logger.LogInformation(
            "RollbackDeploymentEndpoint: Received rollback request for deployment {DeploymentId} with SessionId: {SessionId}",
            deploymentId, req.SessionId ?? "(null)");

        var response = await _mediator.Send(
            new RollbackDeploymentCommand(environmentId, deploymentId, req.SessionId), ct);

        if (!response.Success)
        {
            var statusCode = response.Message?.Contains("not found") == true
                ? StatusCodes.Status404NotFound
                : StatusCodes.Status400BadRequest;
            ThrowError(response.Message ?? "Rollback failed", statusCode);
        }

        Response = response;
    }
}

/// <summary>
/// Request for rollback endpoint.
/// SessionId is optional - if provided, progress notifications will be sent to this session.
/// </summary>
public class RollbackDeploymentRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string? SessionId { get; set; }
}
