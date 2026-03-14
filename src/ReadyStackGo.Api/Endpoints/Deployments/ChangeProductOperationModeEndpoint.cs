using FastEndpoints;
using MediatR;
using Microsoft.AspNetCore.Http;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.ChangeProductOperationMode;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for changing the operation mode of a product deployment.
/// </summary>
public class ChangeProductOperationModeApiRequest
{
    /// <summary>
    /// Environment ID for RBAC scope check (from route).
    /// </summary>
    public string? EnvironmentId { get; set; }

    /// <summary>
    /// Target operation mode: Normal or Maintenance.
    /// </summary>
    public string Mode { get; set; } = string.Empty;

    /// <summary>
    /// Optional reason for the mode change.
    /// </summary>
    public string? Reason { get; set; }

    /// <summary>
    /// Source of the mode change: "Manual" (default) or "Observer".
    /// </summary>
    public string? Source { get; set; }
}

/// <summary>
/// PUT /api/environments/{environmentId}/product-deployments/{productDeploymentId}/operation-mode
/// Changes the operation mode of a product deployment (Normal ↔ Maintenance).
/// Entering maintenance stops containers of all child stacks.
/// Exiting maintenance starts containers of all child stacks.
/// </summary>
[RequirePermission("Deployments", "Write")]
public class ChangeProductOperationModeEndpoint
    : Endpoint<ChangeProductOperationModeApiRequest, ChangeProductOperationModeResponse>
{
    private readonly IMediator _mediator;

    public ChangeProductOperationModeEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Put("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/operation-mode");
        PreProcessor<RbacPreProcessor<ChangeProductOperationModeApiRequest>>();
    }

    public override async Task HandleAsync(ChangeProductOperationModeApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;

        var command = new ChangeProductOperationModeCommand(
            environmentId,
            productDeploymentId,
            req.Mode,
            req.Reason,
            req.Source ?? "Manual");

        var response = await _mediator.Send(command, ct);

        if (!response.Success)
        {
            if (response.Message?.Contains("not found") == true)
            {
                ThrowError(response.Message, StatusCodes.Status404NotFound);
            }

            if (response.Message?.Contains("Cannot exit maintenance") == true ||
                response.Message?.Contains("Cannot change operation mode") == true)
            {
                ThrowError(response.Message, StatusCodes.Status409Conflict);
            }

            ThrowError(response.Message ?? "Failed to change operation mode", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
