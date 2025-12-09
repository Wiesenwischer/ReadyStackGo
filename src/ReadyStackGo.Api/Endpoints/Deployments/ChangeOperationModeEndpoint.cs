using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.ChangeOperationMode;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// PUT /api/deployments/{deploymentId}/operation-mode - Change the operation mode of a deployment.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator.
/// </summary>
[RequirePermission("Deployments", "Write")]
public class ChangeOperationModeEndpoint : Endpoint<ChangeOperationModeRequest, ChangeOperationModeResponse>
{
    private readonly IMediator _mediator;

    public ChangeOperationModeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/deployments/{deploymentId}/operation-mode");
        PreProcessor<RbacPreProcessor<ChangeOperationModeRequest>>();
    }

    public override async Task HandleAsync(ChangeOperationModeRequest req, CancellationToken ct)
    {
        var command = new ChangeOperationModeCommand(
            req.DeploymentId,
            req.Mode,
            req.Reason,
            req.TargetVersion);

        var response = await _mediator.Send(command, ct);

        if (!response.Success)
        {
            ThrowError(response.Message ?? "Failed to change operation mode", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}

/// <summary>
/// Request to change operation mode.
/// </summary>
public class ChangeOperationModeRequest
{
    /// <summary>
    /// Deployment ID (from route).
    /// </summary>
    public string DeploymentId { get; set; } = string.Empty;

    /// <summary>
    /// Target operation mode: Normal, Migrating, Maintenance, or Failed.
    /// Note: Stopped is set via stop deployment endpoint.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Optional reason for the mode change.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Target version (required when entering Migrating mode).
    /// </summary>
    public string? TargetVersion { get; set; }
}
