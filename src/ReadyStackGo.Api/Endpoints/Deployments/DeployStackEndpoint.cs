using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Deployments.DeployStack;

namespace ReadyStackGo.API.Endpoints.Deployments;

/// <summary>
/// Request DTO for deploying a stack from the catalog.
/// </summary>
public class DeployStackApiRequest
{
    /// <summary>
    /// Stack ID from the catalog (format: sourceId:stackName).
    /// </summary>
    public required string StackId { get; set; }

    /// <summary>
    /// Name for this deployment.
    /// </summary>
    public required string StackName { get; set; }

    /// <summary>
    /// Resolved environment variable values.
    /// </summary>
    public Dictionary<string, string> Variables { get; set; } = new();

    /// <summary>
    /// Client-generated session ID for real-time progress tracking via SignalR.
    /// </summary>
    public string? SessionId { get; set; }

    // RBAC scope fields (set from route)
    public string? EnvironmentId { get; set; }
}

/// <summary>
/// Deploys a stack from the catalog. Requires Deployments.Create permission.
/// Uses stackId instead of raw YAML content.
/// </summary>
[RequirePermission("Deployments", "Create")]
public class DeployStackEndpoint : Endpoint<DeployStackApiRequest, DeployStackResponse>
{
    private readonly IMediator _mediator;

    public DeployStackEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/environments/{environmentId}/stacks/{stackId}/deploy");
        PreProcessor<RbacPreProcessor<DeployStackApiRequest>>();
    }

    public override async Task HandleAsync(DeployStackApiRequest req, CancellationToken ct)
    {
        var environmentId = Route<string>("environmentId")!;
        var stackId = Route<string>("stackId")!;

        // Set for RBAC scope check
        req.EnvironmentId = environmentId;
        req.StackId = stackId;

        var response = await _mediator.Send(
            new DeployStackCommand(
                environmentId,
                stackId,
                req.StackName,
                req.Variables,
                req.SessionId),
            ct);

        if (!response.Success && response.Message?.Contains("not found") == true)
        {
            ThrowError(response.Message);
        }

        Response = response;
    }
}
