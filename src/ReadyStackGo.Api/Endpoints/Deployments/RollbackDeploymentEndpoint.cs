using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.RollbackDeployment;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// POST /api/environments/{environmentId}/deployments/{deploymentId}/rollback - Rollback a deployment to a snapshot.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Write")]
public class RollbackDeploymentEndpoint : Endpoint<RollbackDeploymentRequest, RollbackDeploymentResponse>
{
    private readonly IMediator _mediator;

    public RollbackDeploymentEndpoint(IMediator mediator)
    {
        _mediator = mediator;
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

        if (string.IsNullOrEmpty(req.SnapshotId))
        {
            ThrowError("SnapshotId is required", StatusCodes.Status400BadRequest);
        }

        var response = await _mediator.Send(
            new RollbackDeploymentCommand(environmentId, deploymentId, req.SnapshotId!), ct);

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

public class RollbackDeploymentRequest
{
    public string EnvironmentId { get; set; } = string.Empty;
    public string? SnapshotId { get; set; }
}
