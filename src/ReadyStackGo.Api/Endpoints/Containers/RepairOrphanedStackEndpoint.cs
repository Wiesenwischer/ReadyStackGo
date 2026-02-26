using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.RepairOrphanedStack;
using ReadyStackGo.Infrastructure.Security.Authentication;

namespace ReadyStackGo.API.Endpoints.Containers;

public class RepairOrphanedStackRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;
}

/// <summary>
/// Repairs an orphaned stack by creating a Deployment record for its running containers.
/// POST /api/containers/orphaned-stacks/{stackName}/repair?environment={envId}
/// Requires Deployments.Create permission.
/// </summary>
[RequirePermission("Deployments", "Create")]
public class RepairOrphanedStackEndpoint : Endpoint<RepairOrphanedStackRequest, RepairOrphanedStackResult>
{
    private readonly IMediator _mediator;

    public RepairOrphanedStackEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/containers/orphaned-stacks/{stackName}/repair");
        PreProcessor<RbacPreProcessor<RepairOrphanedStackRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(RepairOrphanedStackRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
            ThrowError("Environment is required");

        var stackName = Route<string>("stackName")!;
        var userId = HttpContext.User.FindFirst(RbacClaimTypes.UserId)?.Value;

        if (string.IsNullOrWhiteSpace(userId))
            ThrowError("User ID not found in claims");

        var result = await _mediator.Send(
            new RepairOrphanedStackCommand(req.Environment, stackName, userId!), ct);

        if (!result.Success)
            ThrowError(result.ErrorMessage ?? "Failed to repair orphaned stack");

        Response = result;
    }
}
