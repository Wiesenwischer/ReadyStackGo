using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// Request DTO for product deployment precheck.
/// </summary>
public class ProductPrecheckApiRequest
{
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    public required string ProductId { get; set; }
    public required string DeploymentName { get; set; }
    public List<ProductPrecheckStackConfigDto> StackConfigs { get; set; } = new();
    public Dictionary<string, string> SharedVariables { get; set; } = new();
}

public class ProductPrecheckStackConfigDto
{
    public required string StackId { get; set; }
    public Dictionary<string, string> Variables { get; set; } = new();
}

/// <summary>
/// Response DTO for product deployment precheck.
/// </summary>
public class ProductPrecheckApiResponse
{
    public bool CanDeploy { get; set; }
    public bool HasErrors { get; set; }
    public bool HasWarnings { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<ProductPrecheckStackResultDto> Stacks { get; set; } = [];
}

public class ProductPrecheckStackResultDto
{
    public string StackId { get; set; } = string.Empty;
    public string StackName { get; set; } = string.Empty;
    public bool CanDeploy { get; set; }
    public bool HasErrors { get; set; }
    public bool HasWarnings { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<PrecheckCheckDto> Checks { get; set; } = [];
}

/// <summary>
/// Runs deployment precheck for all stacks in a product.
/// POST /api/environments/{environmentId}/product-deployments/precheck
/// </summary>
[RequirePermission("Deployments", "Create")]
public class ProductPrecheckEndpoint : Endpoint<ProductPrecheckApiRequest, ProductPrecheckApiResponse>
{
    private readonly IMediator _mediator;

    public ProductPrecheckEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/product-deployments/precheck");
        PreProcessor<RbacPreProcessor<ProductPrecheckApiRequest>>();
    }

    public override async Task HandleAsync(ProductPrecheckApiRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RunProductPrecheckQuery(
                req.EnvironmentId,
                req.ProductId,
                req.DeploymentName,
                req.StackConfigs.Select(s => new ProductPrecheckStackConfig(
                    s.StackId,
                    s.Variables)).ToList(),
                req.SharedVariables),
            ct);

        Response = MapToResponse(result);
    }

    private static ProductPrecheckApiResponse MapToResponse(ProductPrecheckResult result)
    {
        return new ProductPrecheckApiResponse
        {
            CanDeploy = result.CanDeploy,
            HasErrors = result.HasErrors,
            HasWarnings = result.HasWarnings,
            Summary = result.Summary,
            Stacks = result.Stacks.Select(s => new ProductPrecheckStackResultDto
            {
                StackId = s.StackId,
                StackName = s.StackName,
                CanDeploy = s.Result.CanDeploy,
                HasErrors = s.Result.HasErrors,
                HasWarnings = s.Result.HasWarnings,
                Summary = s.Result.Summary,
                Checks = s.Result.Checks.Select(c => new PrecheckCheckDto
                {
                    Rule = c.Rule,
                    Severity = c.Severity.ToString().ToLowerInvariant(),
                    Title = c.Title,
                    Detail = c.Detail,
                    ServiceName = c.ServiceName
                }).ToList()
            }).ToList()
        };
    }
}
