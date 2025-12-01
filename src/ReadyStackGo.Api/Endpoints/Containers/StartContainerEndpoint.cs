using FastEndpoints;
using MediatR;
using ReadyStackGo.Api.Authorization;
using ReadyStackGo.Application.UseCases.Containers.StartContainer;

namespace ReadyStackGo.API.Endpoints.Containers;

public class StartContainerRequest
{
    [QueryParam]
    public string Environment { get; set; } = null!;

    /// <summary>
    /// Environment ID for RBAC scope check (alias for Environment).
    /// </summary>
    public string? EnvironmentId => Environment;
}

/// <summary>
/// Starts a container. Requires Deployments.Update permission.
/// Accessible by: SystemAdmin, OrganizationOwner, Operator (scoped).
/// </summary>
[RequirePermission("Deployments", "Update")]
public class StartContainerEndpoint : Endpoint<StartContainerRequest, EmptyResponse>
{
    private readonly IMediator _mediator;

    public StartContainerEndpoint(IMediator mediator)
    {
        _mediator = mediator;
    }

    public override void Configure()
    {
        Post("/api/containers/{id}/start");
        PreProcessor<RbacPreProcessor<StartContainerRequest>>();
        Description(b => b.WithTags("Containers"));
    }

    public override async Task HandleAsync(StartContainerRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Environment))
        {
            ThrowError("Environment is required");
        }

        var id = Route<string>("id")!;

        var result = await _mediator.Send(new StartContainerCommand(req.Environment, id), ct);

        if (!result.Success)
        {
            ThrowError(result.ErrorMessage ?? "Failed to start container");
        }
    }
}
