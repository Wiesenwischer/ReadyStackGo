using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Application.UseCases.Deployments.RetryProduct;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for retrying a failed product deployment.
/// </summary>
public class RetryProductApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    [BindFrom("productDeploymentId")]
    public string ProductDeploymentId { get; set; } = string.Empty;

    public string? SessionId { get; set; }
    public bool ContinueOnError { get; set; } = true;
}

/// <summary>
/// Retries failed stacks of a product deployment. Requires Deployments.Execute permission.
/// POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/retry
/// </summary>
[RequirePermission("Deployments", "Execute")]
public class RetryProductEndpoint : Endpoint<RetryProductApiRequest, DeployProductResponse>
{
    private readonly IMediator _mediator;

    public RetryProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/retry");
        PreProcessor<RbacPreProcessor<RetryProductApiRequest>>();
    }

    public override async Task HandleAsync(RetryProductApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;
        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        var command = new RetryProductCommand(
            environmentId,
            productDeploymentId,
            req.SessionId,
            req.ContinueOnError,
            userId);

        var response = await _mediator.Send(command, ct);

        if (!response.Success)
        {
            if (response.Message?.Contains("not found") == true)
            {
                ThrowError(response.Message, StatusCodes.Status404NotFound);
            }

            ThrowError(response.Message ?? "Retry failed", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
