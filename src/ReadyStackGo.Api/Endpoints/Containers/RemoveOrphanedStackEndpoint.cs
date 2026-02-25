using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.RemoveOrphanedStack;

namespace ReadyStackGo.API.Endpoints.Containers;

public class RemoveOrphanedStackRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    public string? EnvironmentId => Environment;
}

/// <summary>
/// Removes all containers of an orphaned stack group.
/// DELETE /api/containers/orphaned-stacks/{stackName}?environment={envId}
/// Requires Deployments.Delete permission.
/// </summary>
[RequirePermission("Deployments", "Delete")]
public class RemoveOrphanedStackEndpoint : Endpoint<RemoveOrphanedStackRequest, RemoveOrphanedStackResult>
{
    private readonly IMediator _mediator;

    public RemoveOrphanedStackEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/containers/orphaned-stacks/{stackName}");
        PreProcessor<RbacPreProcessor<RemoveOrphanedStackRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(RemoveOrphanedStackRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
            ThrowError("Environment is required");

        var stackName = Route<string>("stackName")!;

        var result = await _mediator.Send(
            new RemoveOrphanedStackCommand(req.Environment, stackName), ct);

        if (!result.Success)
            ThrowError(result.ErrorMessage ?? "Failed to remove orphaned stack");

        Response = result;
    }
}
