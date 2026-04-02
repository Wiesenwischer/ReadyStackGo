using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.Precheck;
using ReadyStackGo.Domain.Deployment.Precheck;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// Request DTO for deployment precheck.
/// </summary>
public class PrecheckApiRequest
{
    /// <summary>
    /// Stack ID from the catalog.
    /// </summary>
    [BindFrom("stackId")]
    public string StackId { get; set; } = string.Empty;

    /// <summary>
    /// Environment ID for deployment.
    /// </summary>
    [BindFrom("environmentId")]
    public string EnvironmentId { get; set; } = string.Empty;

    /// <summary>
    /// Name for this deployment.
    /// </summary>
    public required string StackName { get; set; }

    /// <summary>
    /// Resolved environment variable values.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();
}

/// <summary>
/// Response DTO for deployment precheck.
/// </summary>
public class PrecheckApiResponse
{
    public bool CanDeploy { get; set; }
    public bool HasErrors { get; set; }
    public bool HasWarnings { get; set; }
    public string Summary { get; set; } = string.Empty;
    public List<PrecheckCheckDto> Checks { get; set; } = [];
}

public class PrecheckCheckDto
{
    public string Rule { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? ServiceName { get; set; }
}

/// <summary>
/// Runs deployment precheck. Requires Deployments.Create permission (same as deploy).
/// </summary>
[RequirePermission("Deployments", "Create")]
public class PrecheckEndpoint : Endpoint<PrecheckApiRequest, PrecheckApiResponse>
{
    private readonly IMediator _mediator;

    public PrecheckEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/stacks/{stackId}/precheck");
        PreProcessor<RbacPreProcessor<PrecheckApiRequest>>();
    }

    public override async Task HandleAsync(PrecheckApiRequest req, CancellationToken ct)
    {
        var result = await _mediator.Send(
            new RunDeploymentPrecheckQuery(
                req.EnvironmentId,
                req.StackId,
                req.StackName,
                req.Variables),
            ct);

        Response = MapToResponse(result);
    }

    private static PrecheckApiResponse MapToResponse(PrecheckResult result)
    {
        return new PrecheckApiResponse
        {
            CanDeploy = result.CanDeploy,
            HasErrors = result.HasErrors,
            HasWarnings = result.HasWarnings,
            Summary = result.Summary,
            Checks = result.Checks.Select(c => new PrecheckCheckDto
            {
                Rule = c.Rule,
                Severity = c.Severity.ToString().ToLowerInvariant(),
                Title = c.Title,
                Detail = c.Detail,
                ServiceName = c.ServiceName
            }).ToList()
        };
    }
}
