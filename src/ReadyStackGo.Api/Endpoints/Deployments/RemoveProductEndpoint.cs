using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.RemoveProduct;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for removing a product deployment.
/// </summary>
public class RemoveProductApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    [BindFrom("productDeploymentId")]
    public string ProductDeploymentId { get; set; } = string.Empty;

    public string? SessionId { get; set; }
}

/// <summary>
/// Removes an entire product deployment (all stacks). Requires Deployments.Delete permission.
/// DELETE /api/environments/{environmentId}/product-deployments/{productDeploymentId}
/// </summary>
[RequirePermission("Deployments", "Delete")]
public class RemoveProductEndpoint : Endpoint<RemoveProductApiRequest, RemoveProductResponse>
{
    private readonly IMediator _mediator;

    public RemoveProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/environments/{environmentId}/product-deployments/{productDeploymentId}");
        PreProcessor<RbacPreProcessor<RemoveProductApiRequest>>();
    }

    public override async Task HandleAsync(RemoveProductApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;
        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        var command = new RemoveProductCommand(
            environmentId,
            productDeploymentId,
            req.SessionId,
            userId);

        var response = await _mediator.Send(command, ct);

        if (!response.Success)
        {
            if (response.Message?.Contains("not found") == true)
            {
                ThrowError(response.Message, StatusCodes.Status404NotFound);
            }

            ThrowError(response.Message ?? "Removal failed", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
