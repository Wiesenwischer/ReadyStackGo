using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Application.UseCases.Deployments.RedeployProduct;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for redeploying a running product deployment.
/// </summary>
public class RedeployProductApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    [BindFrom("productDeploymentId")]
    public string ProductDeploymentId { get; set; } = string.Empty;

    public List<string>? StackNames { get; set; }
    public Dictionary<string, string>? Variables { get; set; }
    public string? SessionId { get; set; }
    public bool ContinueOnError { get; set; } = true;
}

/// <summary>
/// Redeploys all or selected stacks of a running product deployment. Requires Deployments.Execute permission.
/// POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/redeploy
/// </summary>
[RequirePermission("Deployments", "Execute")]
public class RedeployProductEndpoint : Endpoint<RedeployProductApiRequest, DeployProductResponse>
{
    private readonly IMediator _mediator;

    public RedeployProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/redeploy");
        PreProcessor<RbacPreProcessor<RedeployProductApiRequest>>();
    }

    public override async Task HandleAsync(RedeployProductApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;
        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        var command = new RedeployProductCommand(
            environmentId,
            productDeploymentId,
            req.StackNames,
            req.Variables,
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

            ThrowError(response.Message ?? "Redeploy failed", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
