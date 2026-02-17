using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.DeployProduct;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// API request for deploying a product.
/// </summary>
public class DeployProductApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    public required string ProductId { get; set; }
    public List<DeployProductStackConfigDto> StackConfigs { get; set; } = new();
    public Dictionary<string, string> SharedVariables { get; set; } = new();
    public string? SessionId { get; set; }
    public bool ContinueOnError { get; set; } = true;
}

public class DeployProductStackConfigDto
{
    public required string StackId { get; set; }
    public required string DeploymentStackName { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

/// <summary>
/// Deploys an entire product (all stacks). Requires Deployments.Create permission.
/// POST /api/environments/{environmentId}/product-deployments
/// </summary>
[RequirePermission("Deployments", "Create")]
public class DeployProductEndpoint : Endpoint<DeployProductApiRequest, DeployProductResponse>
{
    private readonly IMediator _mediator;

    public DeployProductEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments");
        PreProcessor<RbacPreProcessor<DeployProductApiRequest>>();
    }

    public override async Task HandleAsync(DeployProductApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        var command = new DeployProductCommand(
            environmentId,
            req.ProductId,
            req.StackConfigs.Select(s => new DeployProductStackConfig(
                s.StackId,
                s.DeploymentStackName,
                s.Variables)).ToList(),
            req.SharedVariables,
            req.SessionId,
            req.ContinueOnError,
            userId);

        var response = await _mediator.Send(command, ct);

        if (!response.Success && response.Message?.Contains("not found") == true)
        {
            ThrowError(response.Message);
        }

        Response = response;
    }
}
