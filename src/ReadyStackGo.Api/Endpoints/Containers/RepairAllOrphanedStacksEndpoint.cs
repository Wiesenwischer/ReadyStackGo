using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.RepairAllOrphanedStacks;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Containers;

public class RepairAllOrphanedStacksRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;
}

/// <summary>
/// Repairs all orphaned stacks by creating Deployment records for their running containers.
/// POST /api/containers/repair-all-orphaned?environment={envId}
/// Requires Deployments.Create permission.
/// </summary>
[RequirePermission("Deployments", "Create")]
public class RepairAllOrphanedStacksEndpoint
    : Endpoint<RepairAllOrphanedStacksRequest, RepairAllOrphanedStacksResult>
{
    private readonly IMediator _mediator;

    public RepairAllOrphanedStacksEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/containers/repair-all-orphaned");
        PreProcessor<RbacPreProcessor<RepairAllOrphanedStacksRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(RepairAllOrphanedStacksRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
            ThrowError("Environment is required");

        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            ThrowError("User ID not found in claims");

        var result = await _mediator.Send(
            new RepairAllOrphanedStacksCommand(req.Environment, userId!), ct);

        if (!result.Success)
            ThrowError(result.ErrorMessage ?? "Failed to repair orphaned stacks");

        Response = result;
    }
}
