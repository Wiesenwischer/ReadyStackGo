using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.RemoveContainer;

namespace ReadyStackGo.API.Endpoints.Containers;

public class RemoveContainerRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    [QueryParam]
    public bool Force { get; set; }

    /// <summary>
    /// Environment ID for RBAC scope check (alias for Environment).
    /// </summary>
    public string? EnvironmentId => Environment;
}

/// <summary>
/// Removes a container. Requires Deployments.Delete permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Delete")]
public class RemoveContainerEndpoint : Endpoint<RemoveContainerRequest, EmptyResponse>
{
    private readonly IMediator _mediator;

    public RemoveContainerEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Delete("/api/containers/{id}");
        PreProcessor<RbacPreProcessor<RemoveContainerRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(RemoveContainerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var id = Route<string>("id")!;

        var result = await _mediator.Send(new RemoveContainerCommand(req.Environment, id, req.Force), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to remove container");
        }
    }
}
