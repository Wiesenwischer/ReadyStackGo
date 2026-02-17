using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.UpgradeProduct;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for upgrading a product deployment to a new version.
/// </summary>
public class UpgradeProductApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    [BindFrom("productDeploymentId")]
    public string ProductDeploymentId { get; set; } = string.Empty;

    public required string TargetProductId { get; set; }
    public List<UpgradeProductStackConfigDto> StackConfigs { get; set; } = new();
    public Dictionary<string, string> SharedVariables { get; set; } = new();
    public string? SessionId { get; set; }
    public bool ContinueOnError { get; set; } = true;
}

public class UpgradeProductStackConfigDto
{
    public required string StackId { get; set; }
    public required string DeploymentStackName { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

/// <summary>
/// Upgrades an entire product deployment to a new version. Requires Deployments.Write permission.
/// POST /api/environments/{environmentId}/product-deployments/{productDeploymentId}/upgrade
/// </summary>
[RequirePermission("Deployments", "Write")]
public class UpgradeProductEndpoint : Endpoint<UpgradeProductApiRequest, UpgradeProductResponse>
{
    private readonly IMediator _mediator;

    public UpgradeProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments/{productDeploymentId}/upgrade");
        PreProcessor<RbacPreProcessor<UpgradeProductApiRequest>>();
    }

    public override async Task HandleAsync(UpgradeProductApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var productDeploymentId = Route<string>("productDeploymentId")!;
        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        var command = new UpgradeProductCommand(
            environmentId,
            productDeploymentId,
            req.TargetProductId,
            req.StackConfigs.Select(s => new UpgradeProductStackConfig(
                s.StackId,
                s.DeploymentStackName,
                s.Variables)).ToList(),
            req.SharedVariables,
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

            ThrowError(response.Message ?? "Upgrade failed", StatusCodes.Status400BadRequest);
        }

        Response = response;
    }
}
